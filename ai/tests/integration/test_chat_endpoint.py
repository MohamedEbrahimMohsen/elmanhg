from collections.abc import Callable
from typing import Any

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.core.errors import ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.main import create_app
from elmanhg_ai.pipelines.chat import load_chat_prompts
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

PayloadBuilder = Callable[..., dict[str, Any]]


async def post_chat(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/chat", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> dict[str, Any]:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    assert body["code"] == code
    return body


async def test_chat_valid_request_returns_reply(
    client: httpx2.AsyncClient,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
    fake_model: FakeModelClient,
) -> None:
    response = await client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert set(body) == {
        "reply",
        "model",
        "promptVersion",
        "inputTokens",
        "outputTokens",
        "stopReason",
        "costUsd",
        "citations",
    }
    assert body["promptVersion"] == "v2"
    assert body["citations"] == []
    assert len(fake_model.requests) == 1


async def test_chat_valid_request_returns_cost_usd(
    client: httpx2.AsyncClient,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
) -> None:
    response = await client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert "costUsd" in body
    assert body["costUsd"] == 0.0


async def test_chat_missing_token_returns_401_problem(
    client: httpx2.AsyncClient, chat_payload: PayloadBuilder, fake_model: FakeModelClient
) -> None:
    response = await client.post("/v1/chat", json=chat_payload())

    assert_problem(response, 401, "UNAUTHENTICATED")
    assert response.headers["WWW-Authenticate"] == "Bearer"
    assert fake_model.requests == []


async def test_chat_wrong_token_returns_401_problem(
    client: httpx2.AsyncClient, chat_payload: PayloadBuilder
) -> None:
    headers = {"Authorization": "Bearer wrong-token-wrong-token-wrong-token-00"}

    response = await client.post("/v1/chat", json=chat_payload(), headers=headers)

    assert_problem(response, 401, "UNAUTHENTICATED")


async def test_chat_invalid_body_returns_400_validation_problem(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], chat_payload: PayloadBuilder
) -> None:
    response = await client.post("/v1/chat", json=chat_payload(message=""), headers=auth_headers)

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0]["field"] == "message"
    assert body["errors"][0]["code"] == "STRING_TOO_SHORT"
    assert '"input"' not in response.text


async def test_chat_history_over_limit_returns_400_problem(
    settings: Settings,
    fake_model: FakeModelClient,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
) -> None:
    app = create_app(
        settings.model_copy(update={"chat_max_history_messages": 0}), model_client=fake_model
    )

    response = await post_chat(app, chat_payload(), auth_headers)

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0] == {
        "field": "history",
        "code": "TOO_MANY_ITEMS",
        "message": "at most 0 history messages",
    }


async def test_chat_model_unavailable_returns_503_problem(
    settings: Settings, auth_headers: dict[str, str], chat_payload: PayloadBuilder
) -> None:
    app = create_app(settings, model_client=FakeModelClient([ModelUnavailableError()]))

    response = await post_chat(app, chat_payload(), auth_headers)

    body = assert_problem(response, 503, "DEPENDENCY_UNAVAILABLE")
    assert "detail" not in body


async def test_chat_model_output_invalid_returns_502_problem(
    settings: Settings, auth_headers: dict[str, str], chat_payload: PayloadBuilder
) -> None:
    app = create_app(settings, model_client=FakeModelClient([ModelOutputInvalidError()]))

    response = await post_chat(app, chat_payload(), auth_headers)

    assert_problem(response, 502, "MODEL_OUTPUT_INVALID")


@pytest.mark.parametrize("started_state", [None, "model_client", "chat_prompts"])
async def test_chat_without_startup_returns_503_problem(
    app: FastAPI,
    fake_model: FakeModelClient,
    auth_headers: dict[str, str],
    chat_payload: PayloadBuilder,
    started_state: str | None,
) -> None:
    if started_state == "model_client":
        app.state.model_client = fake_model
    if started_state == "chat_prompts":
        app.state.chat_prompts = load_chat_prompts("v1")
    transport = httpx2.ASGITransport(app=app)
    async with httpx2.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    assert_problem(response, 503, "SERVICE_NOT_READY")
    assert fake_model.requests == []


async def test_chat_with_sources_returns_citations(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], chat_payload: PayloadBuilder
) -> None:
    sources = [
        {"reference": "explanation-1", "title": "الشرح — قانون أوم", "content": "V = I R"},
        {"reference": "summary-1", "title": "الملخص", "content": "R = V / I"},
    ]

    response = await client.post(
        "/v1/chat", json=chat_payload(sources=sources), headers=auth_headers
    )

    assert response.status_code == 200
    assert response.json()["citations"] == ["explanation-1"]


async def test_chat_duplicate_source_references_returns_400_problem(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], chat_payload: PayloadBuilder
) -> None:
    sources = [
        {"reference": "summary-1", "title": "الملخص", "content": "a"},
        {"reference": "summary-1", "title": "الملخص", "content": "b"},
    ]

    response = await client.post(
        "/v1/chat", json=chat_payload(sources=sources), headers=auth_headers
    )

    assert_problem(response, 400, "VALIDATION_FAILED")
