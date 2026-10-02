import json
import time
from collections.abc import Mapping
from dataclasses import dataclass
from decimal import Decimal
from typing import Final, Literal

import structlog

from elmanhg_ai.api.chat.schemas import ChatIn, ChatRole
from elmanhg_ai.clients.citations import SOURCES_TAG
from elmanhg_ai.clients.model import (
    ModelClient,
    ModelMessage,
    ModelRequest,
    ModelSource,
    estimate_cost_usd,
)
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.prompts.delimiters import delimiter_pattern, strip_fields, strip_tags
from elmanhg_ai.prompts.loader import Prompt, load_prompt, render
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "avatar_chat"
SYSTEM_PROMPT: Final = "avatar_system"
TURN_PROMPT: Final = "avatar_turn"
DELIMITER_TAG: Final = delimiter_pattern("lesson_context", "student_message", SOURCES_TAG)
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
    cost_usd: Decimal
    citations: tuple[str, ...] = ()


def load_chat_prompts(version: str) -> ChatPrompts:
    return ChatPrompts(
        system=load_prompt(SYSTEM_PROMPT, version), turn=load_prompt(TURN_PROMPT, version)
    )


def strip_delimiters(text: str) -> str:
    return strip_tags(text, DELIMITER_TAG)


def _limit_errors(chat: ChatIn, context_json: str, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    if len(chat.history) > settings.chat_max_history_messages:
        limit = settings.chat_max_history_messages
        errors.append(FieldError("history", "TOO_MANY_ITEMS", f"at most {limit} history messages"))
    if len(chat.message) > settings.chat_max_message_chars:
        limit = settings.chat_max_message_chars
        errors.append(FieldError("message", "TOO_LONG", f"at most {limit} characters"))
    for index, turn in enumerate(chat.history):
        if len(turn.content) > settings.chat_max_message_chars:
            limit = settings.chat_max_message_chars
            errors.append(
                FieldError(f"history[{index}].content", "TOO_LONG", f"at most {limit} characters")
            )
    if len(context_json) > settings.chat_max_context_chars:
        limit = settings.chat_max_context_chars
        errors.append(FieldError("context", "TOO_LONG", f"at most {limit} characters"))
    if len(chat.sources) > settings.chat_max_sources:
        limit = settings.chat_max_sources
        errors.append(FieldError("sources", "TOO_MANY_ITEMS", f"at most {limit} sources"))
    for index, source in enumerate(chat.sources):
        if len(source.content) > settings.chat_max_source_chars:
            limit = settings.chat_max_source_chars
            errors.append(
                FieldError(f"sources[{index}].content", "TOO_LONG", f"at most {limit} characters")
            )
    return errors


async def run(
    chat: ChatIn, *, model: ModelClient, prompts: ChatPrompts, settings: Settings
) -> ChatResult:
    context_json = chat.context.model_dump_json(by_alias=True, exclude_none=True)
    errors = _limit_errors(chat, context_json, settings)
    if errors:
        raise ValidationFailedError(errors)
    context = chat.context.model_dump(mode="json", by_alias=True, exclude_none=True)
    safe_context = json.dumps(
        strip_fields(context, DELIMITER_TAG), ensure_ascii=False, separators=(",", ":")
    )
    turn = render(
        prompts.turn.text, {"context": safe_context, "message": strip_delimiters(chat.message)}
    )
    sources = tuple(
        ModelSource(
            reference=s.reference,
            title=strip_delimiters(s.title),
            content=strip_delimiters(s.content),
        )
        for s in chat.sources
    )
    request = ModelRequest(
        system=prompts.system.text,
        messages=(
            *(
                ModelMessage(role=MODEL_ROLES[m.role], content=strip_delimiters(m.content))
                for m in chat.history
            ),
            ModelMessage(role="user", content=turn),
        ),
        max_tokens=settings.chat_max_tokens,
        sources=sources,
    )
    started = time.perf_counter()
    reply = await model.complete(request)
    latency_ms = round((time.perf_counter() - started) * 1000)
    known = {s.reference for s in chat.sources}
    citations = tuple(dict.fromkeys(c for c in reply.citations if c in known))
    cost = estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings)
    logger.info(
        "chat.completed",
        pipeline=PIPELINE_NAME,
        prompt_version=prompts.version,
        model=reply.model,
        tokens_in=reply.input_tokens,
        tokens_out=reply.output_tokens,
        latency_ms=latency_ms,
        cost_usd=float(cost),
        stop_reason=reply.stop_reason,
        sources=len(sources),
        citations=len(citations),
    )
    return ChatResult(
        reply=reply.text,
        model=reply.model,
        prompt_version=prompts.version,
        input_tokens=reply.input_tokens,
        output_tokens=reply.output_tokens,
        stop_reason=reply.stop_reason,
        cost_usd=cost,
        citations=citations,
    )
