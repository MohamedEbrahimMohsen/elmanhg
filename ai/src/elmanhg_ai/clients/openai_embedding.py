import asyncio
from collections.abc import Awaitable, Callable
from typing import Final, Self

import httpx2
import structlog
from pydantic import BaseModel, ConfigDict, ValidationError

from elmanhg_ai.clients.embedding import EmbeddingReply, EmbeddingRequest
from elmanhg_ai.clients.openai_http import OPENAI_BASE_URL, PROVIDER, post_with_retries
from elmanhg_ai.core.errors import ModelOutputInvalidError
from elmanhg_ai.settings import Settings

EMBEDDINGS_PATH: Final = "/embeddings"

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
        response = await post_with_retries(
            lambda: self._http.post(EMBEDDINGS_PATH, json=payload),
            max_retries=self._max_retries,
            sleep=self._sleep,
            failure_event="embedding.call_failed",
            model=self._model,
        )
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

    async def aclose(self) -> None:
        await self._http.aclose()
