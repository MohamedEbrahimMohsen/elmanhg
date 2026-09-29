import time
from decimal import Decimal
from typing import Final, Literal

from opentelemetry.metrics import Meter
from opentelemetry.trace import Span, SpanKind, Tracer
from opentelemetry.util.types import AttributeValue

from elmanhg_ai.clients.embedding import (
    EmbeddingClient,
    EmbeddingReply,
    EmbeddingRequest,
    estimate_embedding_cost_usd,
)
from elmanhg_ai.clients.model import ModelClient, ModelReply, ModelRequest, estimate_cost_usd
from elmanhg_ai.core.errors import DomainError
from elmanhg_ai.settings import Settings

OPERATION: Final = "gen_ai.operation.name"
PROVIDER: Final = "gen_ai.provider.name"
MODEL: Final = "gen_ai.request.model"
USAGE_KIND: Final = "gen_ai.token.type"
ERROR_TYPE: Final = "error.type"
USAGE_INPUT_TOKENS: Final = "gen_ai.usage.input_tokens"
USAGE_OUTPUT_TOKENS: Final = "gen_ai.usage.output_tokens"

Operation = Literal["chat", "embeddings"]


class AiMetrics:
    def __init__(self, meter: Meter) -> None:
        self._duration = meter.create_histogram(
            "gen_ai.client.operation.duration", unit="s", description="Model call duration."
        )
        self._tokens = meter.create_histogram(
            "gen_ai.client.token.usage", unit="{token}", description="Tokens used per model call."
        )
        self._cost = meter.create_counter(
            "elmanhg.ai.cost", unit="{USD}", description="Estimated model spend in US dollars."
        )

    def record(
        self,
        *,
        operation: Operation,
        provider: str,
        model: str,
        duration_seconds: float,
        input_tokens: int | None,
        output_tokens: int | None,
        cost_usd: Decimal | None,
        error_type: str | None,
    ) -> None:
        attributes: dict[str, AttributeValue] = {
            OPERATION: operation,
            PROVIDER: provider,
            MODEL: model,
        }
        if error_type is not None:
            attributes[ERROR_TYPE] = error_type
        self._duration.record(duration_seconds, attributes)
        for token_type, count in (("input", input_tokens), ("output", output_tokens)):
            if count is not None:
                self._tokens.record(count, attributes | {USAGE_KIND: token_type})
        if cost_usd is not None:
            self._cost.add(float(cost_usd), attributes)


def error_type_of(error: Exception) -> str:
    return error.code.value if isinstance(error, DomainError) else type(error).__name__


def _describe(span: Span, operation: Operation, provider: str, model: str) -> None:
    span.set_attributes({OPERATION: operation, PROVIDER: provider, MODEL: model})


class MeteredModelClient:
    def __init__(
        self, inner: ModelClient, *, metrics: AiMetrics, tracer: Tracer, settings: Settings
    ) -> None:
        self._inner = inner
        self._metrics = metrics
        self._tracer = tracer
        self._settings = settings

    async def complete(self, request: ModelRequest) -> ModelReply:
        settings = self._settings
        model = settings.chat_model
        with self._tracer.start_as_current_span(f"chat {model}", kind=SpanKind.CLIENT) as span:
            _describe(span, "chat", settings.llm_provider, model)
            started = time.perf_counter()
            try:
                reply = await self._inner.complete(request)
            except Exception as error:
                self._metrics.record(
                    operation="chat",
                    provider=settings.llm_provider,
                    model=model,
                    duration_seconds=time.perf_counter() - started,
                    input_tokens=None,
                    output_tokens=None,
                    cost_usd=None,
                    error_type=error_type_of(error),
                )
                raise
            self._metrics.record(
                operation="chat",
                provider=settings.llm_provider,
                model=model,
                duration_seconds=time.perf_counter() - started,
                input_tokens=reply.input_tokens,
                output_tokens=reply.output_tokens,
                cost_usd=estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings),
                error_type=None,
            )
            span.set_attributes(
                {USAGE_INPUT_TOKENS: reply.input_tokens, USAGE_OUTPUT_TOKENS: reply.output_tokens}
            )
            return reply

    async def aclose(self) -> None:
        await self._inner.aclose()


class MeteredEmbeddingClient:
    def __init__(
        self, inner: EmbeddingClient, *, metrics: AiMetrics, tracer: Tracer, settings: Settings
    ) -> None:
        self._inner = inner
        self._metrics = metrics
        self._tracer = tracer
        self._settings = settings

    async def embed(self, request: EmbeddingRequest) -> EmbeddingReply:
        settings = self._settings
        model = settings.embedding_model
        with self._tracer.start_as_current_span(
            f"embeddings {model}", kind=SpanKind.CLIENT
        ) as span:
            _describe(span, "embeddings", settings.embedding_provider, model)
            started = time.perf_counter()
            try:
                reply = await self._inner.embed(request)
            except Exception as error:
                self._metrics.record(
                    operation="embeddings",
                    provider=settings.embedding_provider,
                    model=model,
                    duration_seconds=time.perf_counter() - started,
                    input_tokens=None,
                    output_tokens=None,
                    cost_usd=None,
                    error_type=error_type_of(error),
                )
                raise
            self._metrics.record(
                operation="embeddings",
                provider=settings.embedding_provider,
                model=model,
                duration_seconds=time.perf_counter() - started,
                input_tokens=reply.input_tokens,
                output_tokens=None,
                cost_usd=estimate_embedding_cost_usd(reply.input_tokens, settings),
                error_type=None,
            )
            span.set_attribute(USAGE_INPUT_TOKENS, reply.input_tokens)
            return reply

    async def aclose(self) -> None:
        await self._inner.aclose()
