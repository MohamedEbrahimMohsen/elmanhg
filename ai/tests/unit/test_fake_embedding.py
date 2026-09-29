import math

import pytest

from elmanhg_ai.clients.embedding import EmbeddingRequest
from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient, fake_vector
from elmanhg_ai.core.errors import ModelUnavailableError

DIMENSIONS = 64
FATHA = chr(0x064E)
SHADDA = chr(0x0651)
ALEF_HAMZA_BELOW = chr(0x0625)
ALEF = chr(0x0627)


def cosine(left: tuple[float, ...], right: tuple[float, ...]) -> float:
    return sum(x * y for x, y in zip(left, right, strict=True))


def test_fake_vector_same_text_is_deterministic_and_unit_length() -> None:
    first = fake_vector("قانون أوم", DIMENSIONS)

    second = fake_vector("قانون أوم", DIMENSIONS)

    assert first == second
    assert math.isclose(math.sqrt(sum(value * value for value in first)), 1.0)


def test_fake_vector_ignores_tashkeel_and_alef_variants() -> None:
    plain = f"{ALEF}لتيار الكهربي"
    decorated = f"{ALEF_HAMZA_BELOW}لت{FATHA}ي{SHADDA}ار الكهربي"

    assert fake_vector(plain, DIMENSIONS) == fake_vector(decorated, DIMENSIONS)


def test_fake_vector_shared_words_score_higher_than_unrelated() -> None:
    query = fake_vector("ما هو قانون أوم", DIMENSIONS)

    related = fake_vector("قانون أوم يربط الجهد بالتيار", DIMENSIONS)
    unrelated = fake_vector("الخلية النباتية لها جدار", DIMENSIONS)

    assert cosine(query, related) > cosine(query, unrelated)


def test_fake_vector_no_tokens_returns_first_axis() -> None:
    vector = fake_vector(" ... ", DIMENSIONS)

    assert vector == (1.0, *([0.0] * (DIMENSIONS - 1)))


async def test_fake_embedding_client_script_raises_scripted_error() -> None:
    client = FakeEmbeddingClient([ModelUnavailableError()])
    request = EmbeddingRequest(texts=("a",), input_type="query", dimensions=DIMENSIONS)

    with pytest.raises(ModelUnavailableError):
        await client.embed(request)

    assert client.requests == [request]
