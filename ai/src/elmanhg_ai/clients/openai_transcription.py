import asyncio
from collections.abc import Awaitable, Callable, Mapping
from typing import Final, Self

import httpx2
import structlog
from pydantic import BaseModel, ConfigDict, ValidationError

from elmanhg_ai.clients.openai_http import OPENAI_BASE_URL, PROVIDER, post_with_retries
from elmanhg_ai.clients.transcription import TranscriptionReply, TranscriptionRequest
from elmanhg_ai.core.errors import ModelOutputInvalidError
from elmanhg_ai.settings import Settings

TRANSCRIPTIONS_PATH: Final = "/audio/transcriptions"
FILE_NAMES: Final[Mapping[str, str]] = {
    "audio/webm": "voice.webm",
    "audio/ogg": "voice.ogg",
    "audio/mp4": "voice.m4a",
}

logger: Final = structlog.stdlib.get_logger(__name__)


class _OpenAiTranscription(BaseModel):
    model_config = ConfigDict(extra="ignore")

    text: str


class OpenAiTranscriptionClient:
    def __init__(
        self,
        http: httpx2.AsyncClient,
        model: str,
        max_retries: int,
        sleep: Callable[[float], Awaitable[None]] = asyncio.sleep,
    ) -> None:
        self._http = http
        self._model = model
        self._max_retries = max_retries
        self._sleep = sleep

    @classmethod
    def from_settings(cls, settings: Settings) -> Self:
        key = settings.openai_api_key
        if key is None:
            raise ValueError("openai_api_key is required when transcription_provider is openai")
        http = httpx2.AsyncClient(
            base_url=OPENAI_BASE_URL,
            headers={"Authorization": f"Bearer {key.get_secret_value()}"},
            timeout=httpx2.Timeout(settings.transcription_timeout_seconds),
        )
        return cls(http, settings.transcription_model, settings.model_max_retries)

    async def transcribe(self, request: TranscriptionRequest) -> TranscriptionReply:
        files = {"file": (FILE_NAMES[request.content_type], request.audio, request.content_type)}
        data = {"model": self._model, "language": request.language, "response_format": "json"}
        response = await post_with_retries(
            lambda: self._http.post(TRANSCRIPTIONS_PATH, files=files, data=data),
            max_retries=self._max_retries,
            sleep=self._sleep,
            failure_event="transcription.call_failed",
            model=self._model,
        )
        try:
            body = _OpenAiTranscription.model_validate_json(response.content)
        except ValidationError as error:
            logger.warning(
                "transcription.output_invalid",
                provider=PROVIDER,
                model=self._model,
                error_type=type(error).__name__,
            )
            raise ModelOutputInvalidError() from error
        return TranscriptionReply(text=body.text, model=self._model)

    async def aclose(self) -> None:
        await self._http.aclose()
