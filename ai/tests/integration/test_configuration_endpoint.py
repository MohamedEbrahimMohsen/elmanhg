from typing import Any

import httpx2
import pytest
from fastapi import FastAPI
from pydantic import SecretStr

from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.fake_transcription import FakeTranscriptionClient
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import DEFAULT_LLM_MODEL, Settings

pytestmark = pytest.mark.integration

REAL_LOOKING_KEY = "not-a-secret-config-probe-7f3a91c2"


async def get_configuration(app: FastAPI, headers: dict[str, str]) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.get("/v1/configuration", headers=headers)


def app_with_secret(settings: Settings, field: str, key: str) -> FastAPI:
    keyed = settings.model_copy(update={field: SecretStr(key)})
    return create_app(
        keyed,
        model_client=FakeModelClient(),
        embedding_client=FakeEmbeddingClient(),
        transcription_client=FakeTranscriptionClient(),
    )


def secret_status(body: dict[str, Any]) -> dict[str, bool]:
    return {secret["key"]: secret["isSet"] for secret in body["secrets"]}


async def test_configuration_valid_token_returns_providers_and_models(
    client: httpx2.AsyncClient, auth_headers: dict[str, str]
) -> None:
    response = await client.get("/v1/configuration", headers=auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert body["llmProvider"] == "fake"
    assert body["chatModel"] == DEFAULT_LLM_MODEL
    assert body["essayGradingModel"] == "claude-sonnet-5"
    assert body["embeddingModel"] == "text-embedding-3-small"
    assert body["transcriptionModel"] == "whisper-1"
    assert body["secrets"] == [
        {"key": "ELMANHG_AI_LLM_API_KEY", "isSet": False},
        {"key": "ELMANHG_AI_OPENAI_API_KEY", "isSet": False},
    ]


async def test_configuration_missing_token_returns_401_problem(client: httpx2.AsyncClient) -> None:
    response = await client.get("/v1/configuration")

    assert response.status_code == 401
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == "UNAUTHENTICATED"


async def test_configuration_key_set_reports_set_without_value(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    response = await get_configuration(
        app_with_secret(settings, "openai_api_key", REAL_LOOKING_KEY), auth_headers
    )

    assert response.status_code == 200
    assert secret_status(response.json())["ELMANHG_AI_OPENAI_API_KEY"] is True
    assert REAL_LOOKING_KEY not in response.text


async def test_configuration_placeholder_key_reports_not_set(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    response = await get_configuration(
        app_with_secret(settings, "openai_api_key", "change-me-openai"), auth_headers
    )

    assert response.status_code == 200
    assert secret_status(response.json())["ELMANHG_AI_OPENAI_API_KEY"] is False


@pytest.mark.parametrize(("key", "expected"), [(REAL_LOOKING_KEY, True), ("change-me-llm", False)])
async def test_configuration_llm_key_reports_only_set_status(
    settings: Settings, auth_headers: dict[str, str], key: str, expected: bool
) -> None:
    response = await get_configuration(app_with_secret(settings, "llm_api_key", key), auth_headers)

    assert response.status_code == 200
    llm_secret = next(
        secret for secret in response.json()["secrets"] if secret["key"] == "ELMANHG_AI_LLM_API_KEY"
    )
    assert llm_secret == {"key": "ELMANHG_AI_LLM_API_KEY", "isSet": expected}
    assert key not in response.text
