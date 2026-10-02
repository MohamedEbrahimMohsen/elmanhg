import asyncio
import json
from collections.abc import Awaitable, Callable
from typing import Final, Self

import httpx2
import structlog
from pydantic import BaseModel, ConfigDict, Field, JsonValue, SecretStr, ValidationError

from elmanhg_ai.clients.citations import extract_citations, sources_json
from elmanhg_ai.clients.model import ModelReply, ModelRequest
from elmanhg_ai.clients.openai_http import post_with_retries
from elmanhg_ai.core.errors import ModelOutputInvalidError
from elmanhg_ai.prompts.loader import load_prompt, render
from elmanhg_ai.settings import Settings

PROVIDER: Final = "openai_compatible"
CHAT_COMPLETIONS_PATH: Final = "/chat/completions"
OUTPUT_SCHEMA_NAME: Final = "structured_output"
SOURCES_PROMPT: Final = "lesson_sources"
JSON_OUTPUT_PROMPT: Final = "json_output"
CLIENT_PROMPT_VERSION: Final = "v1"
MISSING_KEY: Final = (
    "llm_api_key or openai_api_key is required when llm_provider is openai_compatible"
)

logger: Final = structlog.stdlib.get_logger(__name__)


class _ChatMessage(BaseModel):
    model_config = ConfigDict(extra="ignore")

    content: str | None = None


class _ChatChoice(BaseModel):
    model_config = ConfigDict(extra="ignore")

    message: _ChatMessage
    finish_reason: str | None = None


class _ChatUsage(BaseModel):
    model_config = ConfigDict(extra="ignore")

    prompt_tokens: int = Field(ge=0)
    completion_tokens: int = Field(ge=0)


class _ChatCompletion(BaseModel):
    model_config = ConfigDict(extra="ignore")

    model: str
    choices: list[_ChatChoice] = Field(min_length=1)
    usage: _ChatUsage


def api_key_for(settings: Settings) -> SecretStr:
    for key in (settings.llm_api_key, settings.openai_api_key):
        if key is not None and key.get_secret_value().strip():
            return key
    raise ValueError(MISSING_KEY)


class OpenAiCompatibleModelClient:
    def __init__(
        self,
        http: httpx2.AsyncClient,
        settings: Settings,
        sleep: Callable[[float], Awaitable[None]] = asyncio.sleep,
    ) -> None:
        self._http = http
        self._settings = settings
        self._sleep = sleep
        self._sources_prompt = load_prompt(SOURCES_PROMPT, CLIENT_PROMPT_VERSION)
        self._json_output_prompt = load_prompt(JSON_OUTPUT_PROMPT, CLIENT_PROMPT_VERSION)

    @classmethod
    def from_settings(cls, settings: Settings) -> Self:
        key = api_key_for(settings)
        http = httpx2.AsyncClient(
            base_url=settings.llm_base_url,
            headers={"Authorization": f"Bearer {key.get_secret_value()}"},
            timeout=httpx2.Timeout(settings.model_timeout_seconds),
        )
        return cls(http, settings)

    async def complete(self, request: ModelRequest) -> ModelReply:
        model = request.model or self._settings.chat_model
        payload = self._payload(request, model)
        timeout = httpx2.Timeout(
            request.timeout_seconds
            if request.timeout_seconds is not None
            else self._settings.model_timeout_seconds
        )
        response = await post_with_retries(
            lambda: self._http.post(CHAT_COMPLETIONS_PATH, json=payload, timeout=timeout),
            max_retries=self._settings.model_max_retries,
            sleep=self._sleep,
            failure_event="model.call_failed",
            model=model,
            provider=PROVIDER,
        )
        try:
            body = _ChatCompletion.model_validate_json(response.content)
        except ValidationError as error:
            logger.warning(
                "model.output_invalid",
                provider=PROVIDER,
                model=model,
                error_type=type(error).__name__,
            )
            raise ModelOutputInvalidError() from error
        choice = body.choices[0]
        text = (choice.message.content or "").strip()
        citations: tuple[str, ...] = ()
        if request.sources and text:
            text, citations = extract_citations(text, [s.reference for s in request.sources])
        if not text:
            logger.warning(
                "model.output_invalid",
                provider=PROVIDER,
                model=body.model,
                stop_reason=choice.finish_reason,
            )
            raise ModelOutputInvalidError()
        return ModelReply(
            text=text,
            model=body.model,
            input_tokens=body.usage.prompt_tokens,
            output_tokens=body.usage.completion_tokens,
            stop_reason=choice.finish_reason,
            citations=citations,
        )

    def _payload(self, request: ModelRequest, model: str) -> dict[str, JsonValue]:
        settings = self._settings
        system = request.system
        response_format: JsonValue = None
        if request.output_schema is not None:
            if settings.llm_structured_output == "json_schema":
                response_format = {
                    "type": "json_schema",
                    "json_schema": {
                        "name": OUTPUT_SCHEMA_NAME,
                        "strict": True,
                        "schema": json.loads(request.output_schema),
                    },
                }
            else:
                response_format = {"type": "json_object"}
                instruction = render(
                    self._json_output_prompt.text, {"schema": request.output_schema}
                )
                system = f"{system}\n\n{instruction}"
        *earlier, last = request.messages
        turn = (
            render(
                self._sources_prompt.text,
                {"sources": sources_json(request.sources), "turn": last.content},
            )
            if request.sources
            else last.content
        )
        messages: list[JsonValue] = [{"role": "system", "content": system}]
        messages.extend({"role": m.role, "content": m.content} for m in earlier)
        messages.append({"role": last.role, "content": turn})
        payload: dict[str, JsonValue] = {
            "model": model,
            "messages": messages,
            settings.llm_max_tokens_field: request.max_tokens,
        }
        if settings.llm_reasoning_effort != "default":
            payload["reasoning_effort"] = settings.llm_reasoning_effort
        if response_format is not None:
            payload["response_format"] = response_format
        return payload

    async def aclose(self) -> None:
        await self._http.aclose()
