import time
from dataclasses import dataclass
from typing import Final

import structlog

from elmanhg_ai.api.transcriptions.schemas import TranscriptionIn
from elmanhg_ai.clients.transcription import (
    TranscriptionClient,
    TranscriptionRequest,
    estimate_transcription_cost_usd,
)
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "transcription"

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class TranscriptionResult:
    text: str
    model: str
    language: str


def _limit_errors(payload: TranscriptionIn, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    size = len(payload.audio)
    if size == 0:
        errors.append(FieldError("audio", "TOO_SHORT", "audio is empty"))
    elif size > settings.transcription_max_audio_bytes:
        limit = settings.transcription_max_audio_bytes
        errors.append(FieldError("audio", "TOO_LARGE", f"at most {limit} bytes"))
    if payload.duration_seconds > settings.transcription_max_duration_seconds:
        limit = settings.transcription_max_duration_seconds
        errors.append(FieldError("durationSeconds", "TOO_LONG", f"at most {limit} seconds"))
    return errors


async def run(
    payload: TranscriptionIn, *, client: TranscriptionClient, settings: Settings
) -> TranscriptionResult:
    errors = _limit_errors(payload, settings)
    if errors:
        raise ValidationFailedError(errors)
    audio = bytes(payload.audio)
    started = time.perf_counter()
    reply = await client.transcribe(
        TranscriptionRequest(audio, payload.content_type.value, payload.language)
    )
    latency_ms = round((time.perf_counter() - started) * 1000)
    text = reply.text.strip()
    logger.info(
        "transcription.completed",
        pipeline=PIPELINE_NAME,
        model=reply.model,
        language=payload.language,
        duration_seconds=payload.duration_seconds,
        audio_bytes=len(audio),
        text_chars=len(text),
        latency_ms=latency_ms,
        cost_usd=float(estimate_transcription_cost_usd(payload.duration_seconds, settings)),
    )
    return TranscriptionResult(text=text, model=reply.model, language=payload.language)
