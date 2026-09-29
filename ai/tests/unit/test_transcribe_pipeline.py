import base64
from decimal import Decimal

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.api.transcriptions.schemas import TranscriptionIn
from elmanhg_ai.clients.fake_transcription import FakeTranscriptionClient
from elmanhg_ai.clients.transcription import TranscriptionReply, estimate_transcription_cost_usd
from elmanhg_ai.core.errors import ModelUnavailableError, ValidationFailedError
from elmanhg_ai.pipelines import transcribe
from elmanhg_ai.settings import Settings

SECRET_TRANSCRIPT = "كلام سري من المعلم"
AUDIO = bytes([0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x02])


def payload(audio: bytes = AUDIO, duration_seconds: int = 12) -> TranscriptionIn:
    return TranscriptionIn.model_validate(
        {
            "audio": base64.b64encode(audio).decode("ascii"),
            "contentType": "audio/ogg",
            "language": "ar",
            "durationSeconds": duration_seconds,
        }
    )


async def test_transcribe_run_success_returns_trimmed_text_and_logs_completion(
    settings: Settings, log_capture: LogCapture
) -> None:
    client = FakeTranscriptionClient([TranscriptionReply(f"  {SECRET_TRANSCRIPT}  ", "whisper-1")])

    result = await transcribe.run(payload(), client=client, settings=settings)

    assert (result.text, result.model, result.language) == (SECRET_TRANSCRIPT, "whisper-1", "ar")
    assert client.requests[0].audio == AUDIO
    assert client.requests[0].content_type == "audio/ogg"
    completed = [e for e in log_capture.entries if e["event"] == "transcription.completed"]
    assert len(completed) == 1
    entry = completed[0]
    assert entry["pipeline"] == "transcription"
    assert (entry["duration_seconds"], entry["audio_bytes"]) == (12, len(AUDIO))
    assert entry["text_chars"] == len(SECRET_TRANSCRIPT)
    assert {"cost_usd", "latency_ms", "model", "language"} <= entry.keys()
    logged = " ".join(str(value) for record in log_capture.entries for value in record.values())
    assert SECRET_TRANSCRIPT not in logged


async def test_transcribe_run_empty_audio_raises_validation_failed(
    settings: Settings, fake_transcription: FakeTranscriptionClient
) -> None:
    with pytest.raises(ValidationFailedError) as error:
        await transcribe.run(payload(audio=b""), client=fake_transcription, settings=settings)

    assert [(e.field, e.code) for e in error.value.errors] == [("audio", "TOO_SHORT")]
    assert fake_transcription.requests == []


async def test_transcribe_run_audio_too_large_raises_validation_failed(
    settings: Settings, fake_transcription: FakeTranscriptionClient
) -> None:
    limited = settings.model_copy(update={"transcription_max_audio_bytes": 4})

    with pytest.raises(ValidationFailedError) as error:
        await transcribe.run(payload(), client=fake_transcription, settings=limited)

    assert [(e.field, e.code) for e in error.value.errors] == [("audio", "TOO_LARGE")]
    assert fake_transcription.requests == []


async def test_transcribe_run_duration_too_long_raises_validation_failed(
    settings: Settings, fake_transcription: FakeTranscriptionClient
) -> None:
    limited = settings.model_copy(update={"transcription_max_duration_seconds": 10})

    with pytest.raises(ValidationFailedError) as error:
        await transcribe.run(payload(), client=fake_transcription, settings=limited)

    assert [(e.field, e.code) for e in error.value.errors] == [("durationSeconds", "TOO_LONG")]


async def test_transcribe_run_client_unavailable_propagates(settings: Settings) -> None:
    client = FakeTranscriptionClient([ModelUnavailableError()])

    with pytest.raises(ModelUnavailableError):
        await transcribe.run(payload(), client=client, settings=settings)


def test_estimate_transcription_cost_usd_uses_per_minute_price(settings: Settings) -> None:
    cost = estimate_transcription_cost_usd(90, settings)

    assert cost == Decimal("0.009000")
    assert str(cost) == "0.009000"
