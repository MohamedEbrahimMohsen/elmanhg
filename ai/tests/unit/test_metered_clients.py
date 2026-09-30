import dataclasses
from collections.abc import Iterator
from dataclasses import dataclass

import pytest
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import (
    HistogramDataPoint,
    InMemoryMetricReader,
    NumberDataPoint,
)
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor
from opentelemetry.sdk.trace.export.in_memory_span_exporter import InMemorySpanExporter
from opentelemetry.trace import SpanKind, StatusCode

from elmanhg_ai.clients.embedding import (
    EmbeddingReply,
    EmbeddingRequest,
    estimate_embedding_cost_usd,
)
from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.fake_transcription import FakeTranscriptionClient
from elmanhg_ai.clients.metered import (
    AiMetrics,
    MeteredEmbeddingClient,
    MeteredModelClient,
    MeteredTranscriptionClient,
)
from elmanhg_ai.clients.model import ModelMessage, ModelReply, ModelRequest, estimate_cost_usd
from elmanhg_ai.clients.transcription import TranscriptionRequest, estimate_transcription_cost_usd
from elmanhg_ai.core.errors import ModelUnavailableError
from elmanhg_ai.settings import Settings

REQUEST = ModelRequest(system="system", messages=(ModelMessage("user", "hi"),), max_tokens=64)
EMBEDDING_REQUEST = EmbeddingRequest(texts=("قانون أوم",), input_type="query", dimensions=8)
TRANSCRIPTION_REQUEST = TranscriptionRequest(
    audio=b"voice", content_type="audio/webm", language="ar", duration_seconds=30
)
REPLY = ModelReply(text="ok", model="m", input_tokens=120, output_tokens=40, stop_reason="end_turn")


@dataclass
class Instruments:
    reader: InMemoryMetricReader
    spans: InMemorySpanExporter
    metrics: AiMetrics
    tracer_provider: TracerProvider

    def points(self, name: str) -> list[HistogramDataPoint | NumberDataPoint]:
        data = self.reader.get_metrics_data()
        if data is None:
            return []
        return [
            point
            for resource in data.resource_metrics
            for scope in resource.scope_metrics
            for metric in scope.metrics
            if metric.name == name
            for point in metric.data.data_points
        ]


@pytest.fixture
def instruments() -> Iterator[Instruments]:
    reader = InMemoryMetricReader()
    meter_provider = MeterProvider(metric_readers=[reader])
    spans = InMemorySpanExporter()
    tracer_provider = TracerProvider()
    tracer_provider.add_span_processor(SimpleSpanProcessor(spans))
    yield Instruments(reader, spans, AiMetrics(meter_provider.get_meter("test")), tracer_provider)
    meter_provider.shutdown()
    tracer_provider.shutdown()


def metered_model(
    inner: FakeModelClient, instruments: Instruments, settings: Settings
) -> MeteredModelClient:
    return MeteredModelClient(
        inner,
        metrics=instruments.metrics,
        tracer=instruments.tracer_provider.get_tracer("test"),
        settings=settings,
    )


def metered_embedding(
    inner: FakeEmbeddingClient, instruments: Instruments, settings: Settings
) -> MeteredEmbeddingClient:
    return MeteredEmbeddingClient(
        inner,
        metrics=instruments.metrics,
        tracer=instruments.tracer_provider.get_tracer("test"),
        settings=settings,
    )


def metered_transcription(
    inner: FakeTranscriptionClient, instruments: Instruments, settings: Settings
) -> MeteredTranscriptionClient:
    return MeteredTranscriptionClient(
        inner,
        metrics=instruments.metrics,
        tracer=instruments.tracer_provider.get_tracer("test"),
        settings=settings,
    )


def token_sums(instruments: Instruments) -> dict[str, float]:
    return {
        str(point.attributes["gen_ai.token.type"]): point.sum
        for point in instruments.points("gen_ai.client.token.usage")
        if isinstance(point, HistogramDataPoint) and point.attributes is not None
    }


async def test_metered_model_success_records_duration_tokens_and_cost(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_model(FakeModelClient([REPLY]), instruments, settings)

    await client.complete(REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert isinstance(duration, HistogramDataPoint)
    assert duration.count == 1
    assert dict(duration.attributes or {}) == {
        "gen_ai.operation.name": "chat",
        "gen_ai.provider.name": "fake",
        "gen_ai.request.model": settings.chat_model,
    }
    assert token_sums(instruments) == {"input": 120, "output": 40}
    [cost] = instruments.points("elmanhg.ai.cost")
    assert isinstance(cost, NumberDataPoint)
    assert cost.value == pytest.approx(float(estimate_cost_usd(120, 40, settings)))


async def test_metered_model_success_creates_client_span_with_usage(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_model(FakeModelClient([REPLY]), instruments, settings)

    await client.complete(REQUEST)

    [span] = instruments.spans.get_finished_spans()
    assert span.name == f"chat {settings.chat_model}"
    assert span.kind == SpanKind.CLIENT
    assert span.attributes is not None
    assert span.attributes["gen_ai.usage.input_tokens"] == 120


async def test_metered_model_failure_records_error_type_and_reraises(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_model(FakeModelClient([ModelUnavailableError()]), instruments, settings)

    with pytest.raises(ModelUnavailableError):
        await client.complete(REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert (duration.attributes or {})["error.type"] == "DEPENDENCY_UNAVAILABLE"
    assert instruments.points("gen_ai.client.token.usage") == []
    [span] = instruments.spans.get_finished_spans()
    assert span.status.status_code == StatusCode.ERROR


async def test_metered_embedding_success_records_input_tokens_and_cost(
    instruments: Instruments, settings: Settings
) -> None:
    reply = EmbeddingReply(model="e", vectors=((1.0,) * 8,), input_tokens=500)
    client = metered_embedding(FakeEmbeddingClient([reply]), instruments, settings)

    await client.embed(EMBEDDING_REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert (duration.attributes or {})["gen_ai.operation.name"] == "embeddings"
    assert (duration.attributes or {})["gen_ai.request.model"] == settings.embedding_model
    assert token_sums(instruments) == {"input": 500}
    [cost] = instruments.points("elmanhg.ai.cost")
    assert isinstance(cost, NumberDataPoint)
    assert cost.value == pytest.approx(float(estimate_embedding_cost_usd(500, settings)))


async def test_metered_embedding_failure_records_error_type_and_reraises(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_embedding(FakeEmbeddingClient([TimeoutError()]), instruments, settings)

    with pytest.raises(TimeoutError):
        await client.embed(EMBEDDING_REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert (duration.attributes or {})["error.type"] == "TimeoutError"


async def test_metered_transcription_success_records_duration_cost_and_span(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_transcription(FakeTranscriptionClient(), instruments, settings)

    await client.transcribe(TRANSCRIPTION_REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert dict(duration.attributes or {}) == {
        "gen_ai.operation.name": "transcription",
        "gen_ai.provider.name": settings.transcription_provider,
        "gen_ai.request.model": settings.transcription_model,
    }
    assert instruments.points("gen_ai.client.token.usage") == []
    [cost] = instruments.points("elmanhg.ai.cost")
    assert isinstance(cost, NumberDataPoint)
    assert cost.value == pytest.approx(float(estimate_transcription_cost_usd(30, settings)))
    [span] = instruments.spans.get_finished_spans()
    assert span.name == f"transcription {settings.transcription_model}"
    assert span.kind == SpanKind.CLIENT


async def test_metered_transcription_failure_records_error_type_and_reraises(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_transcription(
        FakeTranscriptionClient([ModelUnavailableError()]), instruments, settings
    )

    with pytest.raises(ModelUnavailableError):
        await client.transcribe(TRANSCRIPTION_REQUEST)

    [duration] = instruments.points("gen_ai.client.operation.duration")
    assert (duration.attributes or {})["error.type"] == "DEPENDENCY_UNAVAILABLE"
    assert instruments.points("elmanhg.ai.cost") == []
    [span] = instruments.spans.get_finished_spans()
    assert span.status.status_code == StatusCode.ERROR


class ClosingModelClient(FakeModelClient):
    def __init__(self) -> None:
        super().__init__()
        self.closed = False

    async def aclose(self) -> None:
        self.closed = True


class ClosingEmbeddingClient(FakeEmbeddingClient):
    def __init__(self) -> None:
        super().__init__()
        self.closed = False

    async def aclose(self) -> None:
        self.closed = True


async def test_metered_clients_aclose_closes_inner(
    instruments: Instruments, settings: Settings
) -> None:
    model = ClosingModelClient()
    embedding = ClosingEmbeddingClient()

    await metered_model(model, instruments, settings).aclose()
    await metered_embedding(embedding, instruments, settings).aclose()

    assert model.closed is True
    assert embedding.closed is True


async def test_metered_model_request_model_override_labels_span_and_metrics(
    instruments: Instruments, settings: Settings
) -> None:
    client = metered_model(FakeModelClient([REPLY]), instruments, settings)

    await client.complete(dataclasses.replace(REQUEST, model="claude-grader"))

    [span] = instruments.spans.get_finished_spans()
    assert span.name == "chat claude-grader"
    assert (span.attributes or {})["gen_ai.request.model"] == "claude-grader"
    [cost] = instruments.points("elmanhg.ai.cost")
    assert (cost.attributes or {})["gen_ai.request.model"] == "claude-grader"
