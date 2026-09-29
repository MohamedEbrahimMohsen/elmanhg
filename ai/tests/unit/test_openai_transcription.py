from collections.abc import Callable

import httpx2
import pytest
from pydantic import SecretStr

from elmanhg_ai.clients.fake_transcription import FakeTranscriptionClient
from elmanhg_ai.clients.openai_http import OPENAI_BASE_URL
from elmanhg_ai.clients.openai_transcription import OpenAiTranscriptionClient
from elmanhg_ai.clients.transcription import (
    TranscriptionClient,
    TranscriptionRequest,
    build_transcription_client,
)
from elmanhg_ai.core.errors import ErrorCode, ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

Handler = Callable[[httpx2.Request], httpx2.Response]

AUDIO = b"\x1a\x45\xdf\xa3voice"
REQUEST = TranscriptionRequest(
    audio=AUDIO, content_type="audio/webm", language="ar", duration_seconds=12
)
MAX_RETRIES = 2


class RecordingSleep:
    def __init__(self) -> None:
        self.delays: list[float] = []

    async def __call__(self, delay: float) -> None:
        self.delays.append(delay)


def client_for(
    handler: Handler, captured: list[httpx2.Request], sleep: RecordingSleep | None = None
) -> OpenAiTranscriptionClient:
    def recording(request: httpx2.Request) -> httpx2.Response:
        request.read()
        captured.append(request)
        return handler(request)

    http = httpx2.AsyncClient(
        base_url=OPENAI_BASE_URL,
        headers={"Authorization": "Bearer test-key"},
        transport=httpx2.MockTransport(recording),
    )
    return OpenAiTranscriptionClient(http, "whisper-1", MAX_RETRIES, sleep or RecordingSleep())


def respond(status_code: int, body: str = "{}") -> httpx2.Response:
    return httpx2.Response(status_code, content=body, headers={"content-type": "application/json"})


def ok(_: httpx2.Request) -> httpx2.Response:
    return respond(200, '{"text": "t"}')


async def test_openai_transcribe_sends_multipart_file_model_language_and_bearer() -> None:
    captured: list[httpx2.Request] = []
    client = client_for(ok, captured)

    await client.transcribe(REQUEST)

    sent = captured[0]
    body = sent.content
    assert sent.method == "POST"
    assert sent.url.path == "/v1/audio/transcriptions"
    assert sent.headers["Authorization"] == "Bearer test-key"
    assert b'filename="voice.webm"' in body
    assert b"Content-Type: audio/webm" in body
    assert AUDIO in body
    for name, value in (("model", b"whisper-1"), ("language", b"ar"), ("response_format", b"json")):
        assert f'name="{name}"'.encode() in body
        assert value in body


async def test_openai_transcribe_mp4_uses_m4a_file_name() -> None:
    captured: list[httpx2.Request] = []
    client = client_for(ok, captured)

    await client.transcribe(TranscriptionRequest(AUDIO, "audio/mp4", "ar", 12))

    assert b'filename="voice.m4a"' in captured[0].content


async def test_openai_transcribe_success_maps_text_and_configured_model(
    openai_fixture: Callable[[str], str],
) -> None:
    body = openai_fixture("transcription_success.json")
    client = client_for(lambda _: respond(200, body), [])

    reply = await client.transcribe(REQUEST)

    assert reply.text == "  أهلا، خلينا نراجع قانون أوم خطوة بخطوة.  "
    assert reply.model == "whisper-1"


async def test_openai_transcribe_rate_limited_then_success_retries_once() -> None:
    captured: list[httpx2.Request] = []
    sleep = RecordingSleep()
    responses = iter([respond(429), respond(200, '{"text": "t"}')])
    client = client_for(lambda _: next(responses), captured, sleep)

    reply = await client.transcribe(REQUEST)

    assert len(captured) == 2
    assert sleep.delays == [0.5]
    assert reply.text == "t"


async def test_openai_transcribe_server_error_exhausts_retries_raises_model_unavailable() -> None:
    captured: list[httpx2.Request] = []
    sleep = RecordingSleep()
    client = client_for(lambda _: respond(503), captured, sleep)

    with pytest.raises(ModelUnavailableError) as error:
        await client.transcribe(REQUEST)

    assert error.value.code == ErrorCode.DEPENDENCY_UNAVAILABLE
    assert len(captured) == MAX_RETRIES + 1
    assert sleep.delays == [0.5, 1.0]


async def test_openai_transcribe_bad_request_raises_without_retry() -> None:
    captured: list[httpx2.Request] = []
    client = client_for(lambda _: respond(400), captured)

    with pytest.raises(ModelUnavailableError):
        await client.transcribe(REQUEST)

    assert len(captured) == 1


async def test_openai_transcribe_transport_error_raises_model_unavailable() -> None:
    def handler(request: httpx2.Request) -> httpx2.Response:
        raise httpx2.ConnectError("refused", request=request)

    client = client_for(handler, [])

    with pytest.raises(ModelUnavailableError):
        await client.transcribe(REQUEST)


async def test_openai_transcribe_malformed_body_raises_model_output_invalid() -> None:
    client = client_for(lambda _: respond(200, "{}"), [])

    with pytest.raises(ModelOutputInvalidError) as error:
        await client.transcribe(REQUEST)

    assert error.value.code == ErrorCode.MODEL_OUTPUT_INVALID


@pytest.mark.parametrize(
    ("provider", "expected"),
    [("fake", FakeTranscriptionClient), ("openai", OpenAiTranscriptionClient)],
    ids=["fake", "openai"],
)
async def test_build_transcription_client_selects_provider(
    settings: Settings, provider: str, expected: type[TranscriptionClient]
) -> None:
    configured = settings.model_copy(
        update={"transcription_provider": provider, "openai_api_key": SecretStr("test-key")}
    )

    client = build_transcription_client(configured)

    assert isinstance(client, expected)
    await client.aclose()
