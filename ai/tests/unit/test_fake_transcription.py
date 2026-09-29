import pytest

from elmanhg_ai.clients.fake_transcription import (
    FAKE_TRANSCRIPT,
    FAKE_TRANSCRIPTION_MODEL,
    FakeTranscriptionClient,
)
from elmanhg_ai.clients.transcription import TranscriptionReply, TranscriptionRequest
from elmanhg_ai.core.errors import ModelUnavailableError

REQUEST = TranscriptionRequest(audio=b"\x1a\x45\xdf\xa3", content_type="audio/webm", language="ar")


async def test_fake_transcription_client_returns_fixed_arabic_text() -> None:
    client = FakeTranscriptionClient()

    reply = await client.transcribe(REQUEST)

    assert (reply.text, reply.model) == (FAKE_TRANSCRIPT, "fake-transcription")
    assert FAKE_TRANSCRIPTION_MODEL == "fake-transcription"
    assert client.requests == [REQUEST]


async def test_fake_transcription_client_script_returns_and_raises_in_order() -> None:
    scripted = TranscriptionReply("scripted", "whisper-1")
    client = FakeTranscriptionClient([scripted, ModelUnavailableError()])

    first = await client.transcribe(REQUEST)
    with pytest.raises(ModelUnavailableError):
        await client.transcribe(REQUEST)

    assert first == scripted
    assert len(client.requests) == 2
