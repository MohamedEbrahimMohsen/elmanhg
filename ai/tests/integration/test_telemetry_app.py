from collections.abc import AsyncIterator, Callable
from typing import Any

import httpx2
import pytest
from fastapi import FastAPI
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import InMemoryMetricReader
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor
from opentelemetry.sdk.trace.export.in_memory_span_exporter import InMemorySpanExporter
from opentelemetry.trace import SpanKind

from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.core.telemetry import Telemetry
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

PayloadBuilder = Callable[..., dict[str, Any]]
TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736"
PARENT_SPAN_ID = "00f067aa0ba902b7"
TRACEPARENT = f"00-{TRACE_ID}-{PARENT_SPAN_ID}-01"

InMemoryTelemetry = tuple[Telemetry, InMemorySpanExporter, InMemoryMetricReader]


@pytest.fixture
def in_memory_telemetry() -> InMemoryTelemetry:
    spans = InMemorySpanExporter()
    tracer_provider = TracerProvider()
    tracer_provider.add_span_processor(SimpleSpanProcessor(spans))
    reader = InMemoryMetricReader()
    meter_provider = MeterProvider(metric_readers=[reader])
    telemetry = Telemetry(
        tracer_provider=tracer_provider, meter_provider=meter_provider, exporting=False
    )
    return telemetry, spans, reader


@pytest.fixture
def telemetry_app(
    settings: Settings,
    fake_model: FakeModelClient,
    fake_embedding: FakeEmbeddingClient,
    in_memory_telemetry: InMemoryTelemetry,
) -> FastAPI:
    telemetry, _, _ = in_memory_telemetry
    return create_app(
        settings, model_client=fake_model, embedding_client=fake_embedding, telemetry=telemetry
    )


@pytest.fixture
async def telemetry_client(telemetry_app: FastAPI) -> AsyncIterator[httpx2.AsyncClient]:
    transport = httpx2.ASGITransport(app=telemetry_app)
    async with (
        telemetry_app.router.lifespan_context(telemetry_app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        yield client


async def test_chat_request_server_span_continues_incoming_trace(
    telemetry_client: httpx2.AsyncClient,
    in_memory_telemetry: InMemoryTelemetry,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
) -> None:
    _, spans, _ = in_memory_telemetry

    response = await telemetry_client.post(
        "/v1/chat", json=chat_payload(), headers=auth_headers | {"traceparent": TRACEPARENT}
    )

    assert response.status_code == 200
    finished = spans.get_finished_spans()
    assert finished
    assert {format(span.context.trace_id, "032x") for span in finished} == {TRACE_ID}
    [server] = [span for span in finished if span.kind == SpanKind.SERVER]
    assert server.parent is not None
    assert format(server.parent.span_id, "016x") == PARENT_SPAN_ID
    assert response.headers["X-Trace-Id"] == TRACE_ID


async def test_chat_request_without_traceparent_header_matches_server_span(
    telemetry_client: httpx2.AsyncClient,
    in_memory_telemetry: InMemoryTelemetry,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
) -> None:
    _, spans, _ = in_memory_telemetry

    response = await telemetry_client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    [server] = [span for span in spans.get_finished_spans() if span.kind == SpanKind.SERVER]
    assert response.headers["X-Trace-Id"] == format(server.context.trace_id, "032x")


async def test_health_request_creates_no_server_span(
    telemetry_client: httpx2.AsyncClient, in_memory_telemetry: InMemoryTelemetry
) -> None:
    _, spans, _ = in_memory_telemetry

    response = await telemetry_client.get("/health")

    assert response.status_code == 200
    assert spans.get_finished_spans() == ()


async def test_chat_request_records_model_metrics_through_app(
    telemetry_client: httpx2.AsyncClient,
    in_memory_telemetry: InMemoryTelemetry,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
) -> None:
    _, _, reader = in_memory_telemetry

    await telemetry_client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    data = reader.get_metrics_data()
    assert data is not None
    operations = [
        (point.attributes or {}).get("gen_ai.operation.name")
        for resource in data.resource_metrics
        for scope in resource.scope_metrics
        for metric in scope.metrics
        if metric.name == "gen_ai.client.operation.duration"
        for point in metric.data.data_points
    ]
    assert "chat" in operations
