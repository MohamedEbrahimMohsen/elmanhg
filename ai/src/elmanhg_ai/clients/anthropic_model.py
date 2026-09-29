from typing import Final, Self

import anthropic
import structlog
from anthropic.types import MessageParam, TextBlock

from elmanhg_ai.clients.model import ModelReply, ModelRequest
from elmanhg_ai.core.errors import ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

PROVIDER: Final = "anthropic"

logger: Final = structlog.stdlib.get_logger(__name__)


class AnthropicModelClient:
    def __init__(self, client: anthropic.AsyncAnthropic, model: str) -> None:
        self._client = client
        self._model = model

    @classmethod
    def from_settings(cls, settings: Settings) -> Self:
        key = settings.anthropic_api_key
        if key is None:
            raise ValueError("anthropic_api_key is required when llm_provider is anthropic")
        return cls(
            anthropic.AsyncAnthropic(
                api_key=key.get_secret_value(),
                timeout=settings.model_timeout_seconds,
                max_retries=settings.model_max_retries,
            ),
            settings.chat_model,
        )

    async def complete(self, request: ModelRequest) -> ModelReply:
        messages: list[MessageParam] = [
            {"role": message.role, "content": message.content} for message in request.messages
        ]
        try:
            message = await self._client.messages.create(
                model=self._model,
                max_tokens=request.max_tokens,
                system=request.system,
                messages=messages,
            )
        except anthropic.APIError as error:
            logger.warning(
                "model.call_failed",
                provider=PROVIDER,
                model=self._model,
                error_type=type(error).__name__,
                status_code=getattr(error, "status_code", None),
            )
            raise ModelUnavailableError() from error
        text = "".join(
            block.text for block in message.content if isinstance(block, TextBlock)
        ).strip()
        if not text:
            logger.warning(
                "model.output_invalid", model=message.model, stop_reason=message.stop_reason
            )
            raise ModelOutputInvalidError()
        return ModelReply(
            text=text,
            model=message.model,
            input_tokens=message.usage.input_tokens,
            output_tokens=message.usage.output_tokens,
            stop_reason=message.stop_reason,
        )

    async def aclose(self) -> None:
        await self._client.close()
