from typing import Any

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.clients.embedding import EmbeddingReply
from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
from elmanhg_ai.core.errors import ModelUnavailableError
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

VALID_PAYLOAD = {"inputType": "document", "texts": ["قانون أوم", "المقاومة"]}


async def post_embeddings(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/embeddings", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> dict[str, Any]:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    assert body["code"] == code
    return body


async def test_embeddings_valid_request_returns_vectors(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], fake_embedding: FakeEmbeddingClient
) -> None:
    response = await client.post("/v1/embeddings", json=VALID_PAYLOAD, headers=auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert set(body) == {"model", "dimensions", "embeddings", "inputTokens"}
    assert len(body["embeddings"]) == len(VALID_PAYLOAD["texts"])
    assert body["dimensions"] == 1536
    assert all(len(vector) == 1536 for vector in body["embeddings"])
    assert len(fake_embedding.requests) == 1


async def test_embeddings_missing_token_returns_401_problem(
    client: httpx2.AsyncClient, fake_embedding: FakeEmbeddingClient
) -> None:
    response = await client.post("/v1/embeddings", json=VALID_PAYLOAD)

    assert_problem(response, 401, "UNAUTHENTICATED")
    assert fake_embedding.requests == []


async def test_embeddings_empty_texts_returns_400_validation_failed(
    client: httpx2.AsyncClient, auth_headers: dict[str, str]
) -> None:
    response = await client.post(
        "/v1/embeddings", json={"inputType": "query", "texts": []}, headers=auth_headers
    )

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0]["field"] == "texts"


async def test_embeddings_too_many_texts_returns_400_too_many_items(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    app = create_app(
        settings.model_copy(update={"embedding_max_texts": 1}),
        embedding_client=FakeEmbeddingClient(),
    )

    response = await post_embeddings(app, VALID_PAYLOAD, auth_headers)

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0]["code"] == "TOO_MANY_ITEMS"


async def test_embeddings_provider_failure_returns_503_dependency_unavailable(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    app = create_app(settings, embedding_client=FakeEmbeddingClient([ModelUnavailableError()]))

    response = await post_embeddings(app, VALID_PAYLOAD, auth_headers)

    assert_problem(response, 503, "DEPENDENCY_UNAVAILABLE")


async def test_embeddings_invalid_output_returns_502(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    vector = tuple([0.0] * settings.embedding_dimensions)
    app = create_app(
        settings, embedding_client=FakeEmbeddingClient([EmbeddingReply("m", (vector,), 1)])
    )

    response = await post_embeddings(app, VALID_PAYLOAD, auth_headers)

    assert_problem(response, 502, "MODEL_OUTPUT_INVALID")
