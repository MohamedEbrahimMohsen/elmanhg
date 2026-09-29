from decimal import Decimal

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.api.embeddings.schemas import EmbeddingsIn
from elmanhg_ai.clients.embedding import EmbeddingReply, estimate_embedding_cost_usd
from elmanhg_ai.clients.fake_embedding import FAKE_EMBEDDING_MODEL, FakeEmbeddingClient
from elmanhg_ai.core.errors import FieldError, ModelOutputInvalidError, ValidationFailedError
from elmanhg_ai.pipelines import embed
from elmanhg_ai.settings import Settings

SECRET_TEXT = "نص سري للطالب"


def payload(*texts: str) -> EmbeddingsIn:
    return EmbeddingsIn.model_validate({"inputType": "document", "texts": list(texts)})


async def test_embed_run_success_returns_vectors_and_logs_completion(
    settings: Settings, fake_embedding: FakeEmbeddingClient, log_capture: LogCapture
) -> None:
    result = await embed.run(payload(SECRET_TEXT, "b"), client=fake_embedding, settings=settings)

    assert result.model == FAKE_EMBEDDING_MODEL
    assert result.dimensions == settings.embedding_dimensions
    assert len(result.vectors) == 2
    assert all(len(vector) == settings.embedding_dimensions for vector in result.vectors)
    assert fake_embedding.requests[0].input_type == "document"
    completed = [e for e in log_capture.entries if e["event"] == "embedding.completed"]
    assert len(completed) == 1
    assert completed[0]["pipeline"] == "embeddings"
    assert {"tokens_in", "cost_usd", "latency_ms", "count", "model"} <= completed[0].keys()
    assert "texts" not in completed[0]
    logged = " ".join(str(value) for entry in log_capture.entries for value in entry.values())
    assert SECRET_TEXT not in logged


async def test_embed_run_too_many_texts_raises_validation_failed(
    settings: Settings, fake_embedding: FakeEmbeddingClient
) -> None:
    limited = settings.model_copy(update={"embedding_max_texts": 1})

    with pytest.raises(ValidationFailedError) as error:
        await embed.run(payload("a", "b"), client=fake_embedding, settings=limited)

    assert error.value.errors == (FieldError("texts", "TOO_MANY_ITEMS", "at most 1 texts"),)
    assert fake_embedding.requests == []


async def test_embed_run_text_too_long_raises_validation_failed(
    settings: Settings, fake_embedding: FakeEmbeddingClient
) -> None:
    limited = settings.model_copy(update={"embedding_max_text_chars": 3})

    with pytest.raises(ValidationFailedError) as error:
        await embed.run(payload("abc", "abcd"), client=fake_embedding, settings=limited)

    assert [(e.field, e.code) for e in error.value.errors] == [("texts[1]", "TOO_LONG")]
    assert fake_embedding.requests == []


async def test_embed_run_wrong_vector_count_raises_model_output_invalid(
    settings: Settings,
) -> None:
    vector = tuple([0.0] * settings.embedding_dimensions)
    client = FakeEmbeddingClient([EmbeddingReply("m", (vector,), 1)])

    with pytest.raises(ModelOutputInvalidError):
        await embed.run(payload("a", "b"), client=client, settings=settings)


async def test_embed_run_wrong_dimensions_raises_model_output_invalid(settings: Settings) -> None:
    client = FakeEmbeddingClient([EmbeddingReply("m", ((0.0, 1.0),), 1)])

    with pytest.raises(ModelOutputInvalidError):
        await embed.run(payload("a"), client=client, settings=settings)


def test_estimate_embedding_cost_usd_uses_configured_price(settings: Settings) -> None:
    cost = estimate_embedding_cost_usd(1_000_000, settings)

    assert cost == Decimal("0.020000")
    assert str(cost) == "0.020000"
