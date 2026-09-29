import asyncio
from collections.abc import Awaitable, Callable
from typing import Final, Self

import httpx2
import structlog
from pydantic import BaseModel, ConfigDict, ValidationError

from elmanhg_ai.clients.embedding import EmbeddingReply, EmbeddingRequest
from elmanhg_ai.core.errors import ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

PROVIDER: Final = "openai"
OPENAI_BASE_URL: Final = "https://api.openai.com/v1"
EMBEDDINGS_PATH: Final = "/embeddings"
RETRY_BASE_SECONDS: Final = 0.5
RETRY_STATUSES: Final = frozenset({429, 500, 502, 503, 504})

logger: Final = structlog.stdlib.get_logger(__name__)


class _OpenAiItem(BaseModel):
    model_config = ConfigDict(extra="ignore")

    index: int
    embedding: list[float]


class _OpenAiUsage(BaseModel):
    model_config = ConfigDict(extra="ignore")

    prompt_tokens: int


class _OpenAiEmbeddings(BaseModel):
    model_config = ConfigDict(extra="ignore")

    data: list[_OpenAiItem]
    model: str
    usage: _OpenAiUsage


class OpenAiEmbeddingClient:
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
            raise ValueError("openai_api_key is required when embedding_provider is openai")
        http = httpx2.AsyncClient(
            base_url=OPENAI_BASE_URL,
            headers={"Authorization": f"Bearer {key.get_secret_value()}"},
            timeout=httpx2.Timeout(settings.model_timeout_seconds),
        )
        return cls(http, settings.embedding_model, settings.model_max_retries)

    async def embed(self, request: EmbeddingRequest) -> EmbeddingReply:
        payload = {
            "model": self._model,
            "input": list(request.texts),
            "dimensions": request.dimensions,
            "encoding_format": "float",
        }
        response = await self._post(payload)
        try:
            body = _OpenAiEmbeddings.model_validate_json(response.content)
        except ValidationError as error:
            logger.warning(
                "embedding.output_invalid",
                provider=PROVIDER,
                model=self._model,
                error_type=type(error).__name__,
            )
            raise ModelOutputInvalidError() from error
        items = sorted(body.data, key=lambda item: item.index)
        return EmbeddingReply(
            model=body.model,
            vectors=tuple(tuple(item.embedding) for item in items),
            input_tokens=body.usage.prompt_tokens,
        )

    async def _post(self, payload: dict[str, object]) -> httpx2.Response:
        status_code: int | None = None
        error_type = "HTTPStatusError"
        for attempt in range(self._max_retries + 1):
            try:
                response = await self._http.post(EMBEDDINGS_PATH, json=payload)
            except httpx2.TransportError as error:
                status_code, error_type = None, type(error).__name__
            else:
                if response.is_success:
                    return response
                status_code, error_type = response.status_code, "HTTPStatusError"
                if status_code not in RETRY_STATUSES:
                    break
            if attempt < self._max_retries:
                await self._sleep(RETRY_BASE_SECONDS * 2**attempt)
        logger.warning(
            "embedding.call_failed",
            provider=PROVIDER,
            model=self._model,
            status_code=status_code,
            error_type=error_type,
        )
        raise ModelUnavailableError()

    async def aclose(self) -> None:
        await self._http.aclose()
