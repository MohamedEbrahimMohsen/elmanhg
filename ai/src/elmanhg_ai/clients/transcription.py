from dataclasses import dataclass
from decimal import Decimal
from typing import Final, Protocol

from elmanhg_ai.clients.model import COST_QUANTUM
from elmanhg_ai.settings import Settings

SECONDS_PER_MINUTE: Final = Decimal(60)


@dataclass(frozen=True, slots=True)
class TranscriptionRequest:
    audio: bytes
    content_type: str
    language: str
    duration_seconds: int


@dataclass(frozen=True, slots=True)
class TranscriptionReply:
    text: str
    model: str


class TranscriptionClient(Protocol):
    async def transcribe(self, request: TranscriptionRequest) -> TranscriptionReply: ...

    async def aclose(self) -> None: ...


def estimate_transcription_cost_usd(duration_seconds: int, settings: Settings) -> Decimal:
    return (duration_seconds * settings.transcription_usd_per_minute / SECONDS_PER_MINUTE).quantize(
        COST_QUANTUM
    )


def build_transcription_client(settings: Settings) -> TranscriptionClient:
    from elmanhg_ai.clients.fake_transcription import FakeTranscriptionClient
    from elmanhg_ai.clients.openai_transcription import OpenAiTranscriptionClient

    match settings.transcription_provider:
        case "fake":
            return FakeTranscriptionClient()
        case "openai":
            return OpenAiTranscriptionClient.from_settings(settings)
