import re
import time
from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final, Literal

import structlog

from elmanhg_ai.api.chat.schemas import ChatIn, ChatRole
from elmanhg_ai.clients.model import (
    ModelClient,
    ModelMessage,
    ModelRequest,
    estimate_cost_usd,
)
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.prompts.loader import Prompt, load_prompt, render
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "avatar_chat"
SYSTEM_PROMPT: Final = "avatar_system"
TURN_PROMPT: Final = "avatar_turn"
DELIMITER_TAG: Final = re.compile(
    r"<\s*(?:/\s*)?(?:lesson_context|student_message)\b[^<>]*>?", re.IGNORECASE
)
MODEL_ROLES: Final[Mapping[ChatRole, Literal["user", "assistant"]]] = {
    ChatRole.USER: "user",
    ChatRole.ASSISTANT: "assistant",
}

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class ChatPrompts:
    system: Prompt
    turn: Prompt

    @property
    def version(self) -> str:
        return self.system.version


@dataclass(frozen=True, slots=True)
class ChatResult:
    reply: str
    model: str
    prompt_version: str
    input_tokens: int
    output_tokens: int
    stop_reason: str | None


def load_chat_prompts(version: str) -> ChatPrompts:
    return ChatPrompts(
        system=load_prompt(SYSTEM_PROMPT, version), turn=load_prompt(TURN_PROMPT, version)
    )


def strip_delimiters(text: str) -> str:
    # Repeat until stable: removing an inner tag can join its neighbours into a new tag.
    stripped = DELIMITER_TAG.sub("", text)
    while stripped != text:
        text = stripped
        stripped = DELIMITER_TAG.sub("", text)
    return stripped


def _limit_errors(chat: ChatIn, context_json: str, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    if len(chat.history) > settings.chat_max_history_messages:
        limit = settings.chat_max_history_messages
        errors.append(FieldError("history", "TOO_MANY_ITEMS", f"at most {limit} history messages"))
    if len(chat.message) > settings.chat_max_message_chars:
        limit = settings.chat_max_message_chars
        errors.append(FieldError("message", "TOO_LONG", f"at most {limit} characters"))
    if len(context_json) > settings.chat_max_context_chars:
        limit = settings.chat_max_context_chars
        errors.append(FieldError("context", "TOO_LONG", f"at most {limit} characters"))
    return errors


async def run(
    chat: ChatIn, *, model: ModelClient, prompts: ChatPrompts, settings: Settings
) -> ChatResult:
    context_json = chat.context.model_dump_json(by_alias=True, exclude_none=True)
    errors = _limit_errors(chat, context_json, settings)
    if errors:
        raise ValidationFailedError(errors)
    turn = render(
        prompts.turn.text,
        {"context": strip_delimiters(context_json), "message": strip_delimiters(chat.message)},
    )
    request = ModelRequest(
        system=prompts.system.text,
        messages=(
            *(ModelMessage(role=MODEL_ROLES[m.role], content=m.content) for m in chat.history),
            ModelMessage(role="user", content=turn),
        ),
        max_tokens=settings.chat_max_tokens,
    )
    started = time.perf_counter()
    reply = await model.complete(request)
    latency_ms = round((time.perf_counter() - started) * 1000)
    logger.info(
        "chat.completed",
        pipeline=PIPELINE_NAME,
        prompt_version=prompts.version,
        model=reply.model,
        tokens_in=reply.input_tokens,
        tokens_out=reply.output_tokens,
        latency_ms=latency_ms,
        cost_usd=float(estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings)),
        stop_reason=reply.stop_reason,
    )
    return ChatResult(
        reply=reply.text,
        model=reply.model,
        prompt_version=prompts.version,
        input_tokens=reply.input_tokens,
        output_tokens=reply.output_tokens,
        stop_reason=reply.stop_reason,
    )
