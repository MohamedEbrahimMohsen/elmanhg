import time
from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final, Literal

import structlog

from elmanhg_ai.api.embeddings.schemas import EmbeddingInputType, EmbeddingsIn
from elmanhg_ai.clients.embedding import (
    EmbeddingClient,
    EmbeddingRequest,
    estimate_embedding_cost_usd,
)
from elmanhg_ai.core.errors import FieldError, ModelOutputInvalidError, ValidationFailedError
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "embeddings"
INPUT_TYPES: Final[Mapping[EmbeddingInputType, Literal["document", "query"]]] = {
    EmbeddingInputType.DOCUMENT: "document",
    EmbeddingInputType.QUERY: "query",
}

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class EmbeddingsResult:
    model: str
    dimensions: int
    vectors: tuple[tuple[float, ...], ...]
    input_tokens: int


def _limit_errors(payload: EmbeddingsIn, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    if len(payload.texts) > settings.embedding_max_texts:
        limit = settings.embedding_max_texts
        errors.append(FieldError("texts", "TOO_MANY_ITEMS", f"at most {limit} texts"))
    limit = settings.embedding_max_text_chars
    errors.extend(
        FieldError(f"texts[{index}]", "TOO_LONG", f"at most {limit} characters")
        for index, text in enumerate(payload.texts)
        if len(text) > limit
    )
    return errors


async def run(
    payload: EmbeddingsIn, *, client: EmbeddingClient, settings: Settings
) -> EmbeddingsResult:
    errors = _limit_errors(payload, settings)
    if errors:
        raise ValidationFailedError(errors)
    dimensions = settings.embedding_dimensions
    started = time.perf_counter()
    reply = await client.embed(
        EmbeddingRequest(tuple(payload.texts), INPUT_TYPES[payload.input_type], dimensions)
    )
    latency_ms = round((time.perf_counter() - started) * 1000)
    if len(reply.vectors) != len(payload.texts) or any(
        len(vector) != dimensions for vector in reply.vectors
    ):
        logger.warning(
            "embedding.output_invalid",
            pipeline=PIPELINE_NAME,
            model=reply.model,
            expected_count=len(payload.texts),
            count=len(reply.vectors),
        )
        raise ModelOutputInvalidError()
    logger.info(
        "embedding.completed",
        pipeline=PIPELINE_NAME,
        model=reply.model,
        input_type=payload.input_type.value,
        count=len(reply.vectors),
        tokens_in=reply.input_tokens,
        latency_ms=latency_ms,
        cost_usd=float(estimate_embedding_cost_usd(reply.input_tokens, settings)),
    )
    return EmbeddingsResult(
        model=reply.model,
        dimensions=dimensions,
        vectors=reply.vectors,
        input_tokens=reply.input_tokens,
    )
