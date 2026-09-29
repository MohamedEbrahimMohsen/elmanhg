import base64
from typing import Any

import pytest
from pydantic import ValidationError

from elmanhg_ai.api.transcriptions.schemas import AudioContentType, TranscriptionIn

AUDIO = bytes([0x1A, 0x45, 0xDF, 0xA3, 0x01])


def payload(**overrides: Any) -> dict[str, Any]:
    body: dict[str, Any] = {
        "audio": base64.b64encode(AUDIO).decode("ascii"),
        "contentType": "audio/webm",
        "language": "ar",
        "durationSeconds": 12,
    }
    return body | overrides


def error_of(body: dict[str, Any]) -> dict[str, Any]:
    with pytest.raises(ValidationError) as error:
        TranscriptionIn.model_validate(body)
    first: dict[str, Any] = dict(error.value.errors()[0])
    return first


def test_transcription_in_valid_payload_decodes_audio() -> None:
    model = TranscriptionIn.model_validate(payload())

    assert model.audio == AUDIO
    assert model.content_type == AudioContentType.WEBM
    assert model.duration_seconds == 12
    assert model.language == "ar"


def test_transcription_in_invalid_base64_raises_at_audio() -> None:
    assert error_of(payload(audio="%%%not-base64%%%"))["loc"] == ("audio",)


def test_transcription_in_unknown_content_type_raises_at_content_type() -> None:
    assert error_of(payload(contentType="audio/mpeg"))["loc"] == ("contentType",)


def test_transcription_in_duration_zero_raises_at_duration_seconds() -> None:
    assert error_of(payload(durationSeconds=0))["loc"] == ("durationSeconds",)


def test_transcription_in_bad_language_raises_at_language() -> None:
    assert error_of(payload(language="arabic"))["loc"] == ("language",)


def test_transcription_in_extra_field_raises_forbidden() -> None:
    assert error_of(payload(prompt="x"))["type"] == "extra_forbidden"
