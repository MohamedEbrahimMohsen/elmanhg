from collections.abc import Callable
from typing import Any

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.clients.fake_transcription import FAKE_TRANSCRIPT, FakeTranscriptionClient
from elmanhg_ai.core.errors import ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

Payload = Callable[..., dict[str, Any]]


async def post_transcription(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/transcriptions", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> dict[str, Any]:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    assert body["code"] == code
    return body


async def test_transcriptions_valid_request_returns_text(
    client: httpx2.AsyncClient,
    auth_headers: dict[str, str],
    transcription_payload: Payload,
    fake_transcription: FakeTranscriptionClient,
) -> None:
    response = await client.post(
        "/v1/transcriptions", json=transcription_payload(), headers=auth_headers
    )

    assert response.status_code == 200
    assert response.json() == {
        "text": FAKE_TRANSCRIPT,
        "model": "fake-transcription",
        "language": "ar",
    }
    assert len(fake_transcription.requests) == 1


async def test_transcriptions_missing_token_returns_401_problem(
    client: httpx2.AsyncClient,
    transcription_payload: Payload,
    fake_transcription: FakeTranscriptionClient,
) -> None:
    response = await client.post("/v1/transcriptions", json=transcription_payload())

    assert_problem(response, 401, "UNAUTHENTICATED")
    assert fake_transcription.requests == []


async def test_transcriptions_invalid_base64_returns_400_validation_failed(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], transcription_payload: Payload
) -> None:
    response = await client.post(
        "/v1/transcriptions", json=transcription_payload(audio="%%%"), headers=auth_headers
    )

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0]["field"] == "audio"


async def test_transcriptions_audio_too_large_returns_400_too_large(
    settings: Settings, auth_headers: dict[str, str], transcription_payload: Payload
) -> None:
    app = create_app(
        settings.model_copy(update={"transcription_max_audio_bytes": 4}),
        transcription_client=FakeTranscriptionClient(),
    )

    response = await post_transcription(app, transcription_payload(), auth_headers)

    body = assert_problem(response, 400, "VALIDATION_FAILED")
    assert body["errors"][0]["code"] == "TOO_LARGE"


async def test_transcriptions_provider_failure_returns_503_dependency_unavailable(
    settings: Settings, auth_headers: dict[str, str], transcription_payload: Payload
) -> None:
    app = create_app(
        settings, transcription_client=FakeTranscriptionClient([ModelUnavailableError()])
    )

    response = await post_transcription(app, transcription_payload(), auth_headers)

    assert_problem(response, 503, "DEPENDENCY_UNAVAILABLE")


async def test_transcriptions_invalid_output_returns_502(
    settings: Settings, auth_headers: dict[str, str], transcription_payload: Payload
) -> None:
    app = create_app(
        settings, transcription_client=FakeTranscriptionClient([ModelOutputInvalidError()])
    )

    response = await post_transcription(app, transcription_payload(), auth_headers)

    assert_problem(response, 502, "MODEL_OUTPUT_INVALID")
