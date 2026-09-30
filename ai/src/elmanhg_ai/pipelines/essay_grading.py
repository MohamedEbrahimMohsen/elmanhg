import json
import time
from dataclasses import dataclass
from decimal import Decimal
from typing import Final

import structlog
from pydantic import JsonValue

from elmanhg_ai.api.essay_grades.schemas import EssayGradeIn
from elmanhg_ai.clients.model import ModelClient, ModelMessage, ModelRequest, estimate_cost_usd
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.pipelines.essay_grading_output import CriterionGrade, parse_model_grade
from elmanhg_ai.prompts.delimiters import delimiter_pattern, strip_fields, strip_tags
from elmanhg_ai.prompts.loader import Prompt, load_output_schema, load_prompt, render
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "essay_grading"
SYSTEM_PROMPT: Final = "essay_grade_system"
TURN_PROMPT: Final = "essay_grade_turn"
OUTPUT_SCHEMA: Final = "essay_grade_output"
DELIMITER_TAG: Final = delimiter_pattern("grading_context", "student_essay")

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class EssayGradingPrompts:
    system: Prompt
    turn: Prompt
    output_schema: str

    @property
    def version(self) -> str:
        return self.system.version


@dataclass(frozen=True, slots=True)
class EssayGradingResult:
    criteria: tuple[CriterionGrade, ...]
    total_points: int
    max_points: int
    justification: str
    confidence: float
    model: str
    prompt_version: str
    input_tokens: int
    output_tokens: int
    stop_reason: str | None
    cost_usd: Decimal


def load_essay_grading_prompts(version: str) -> EssayGradingPrompts:
    return EssayGradingPrompts(
        system=load_prompt(SYSTEM_PROMPT, version),
        turn=load_prompt(TURN_PROMPT, version),
        output_schema=load_output_schema(OUTPUT_SCHEMA, version),
    )


def _limit_errors(payload: EssayGradeIn, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    field_limit = settings.essay_grading_max_field_chars
    if len(payload.essay) > settings.essay_grading_max_essay_chars:
        limit = settings.essay_grading_max_essay_chars
        errors.append(FieldError("essay", "TOO_LONG", f"at most {limit} characters"))
    if len(payload.question) > field_limit:
        errors.append(FieldError("question", "TOO_LONG", f"at most {field_limit} characters"))
    if len(payload.criteria) > settings.essay_grading_max_criteria:
        limit = settings.essay_grading_max_criteria
        errors.append(FieldError("criteria", "TOO_MANY_ITEMS", f"at most {limit} criteria"))
    if len(payload.model_answers) > settings.essay_grading_max_model_answers:
        limit = settings.essay_grading_max_model_answers
        errors.append(
            FieldError("modelAnswers", "TOO_MANY_ITEMS", f"at most {limit} model answers")
        )
    for index, answer in enumerate(payload.model_answers):
        if len(answer) > field_limit:
            errors.append(
                FieldError(
                    f"modelAnswers[{index}]", "TOO_LONG", f"at most {field_limit} characters"
                )
            )
    if len(payload.objectives) > settings.essay_grading_max_objectives:
        limit = settings.essay_grading_max_objectives
        errors.append(FieldError("objectives", "TOO_MANY_ITEMS", f"at most {limit} objectives"))
    return errors


def _context(payload: EssayGradeIn) -> dict[str, JsonValue]:
    context: dict[str, JsonValue] = {
        "question": payload.question,
        "criteria": [
            criterion.model_dump(mode="json", by_alias=True, exclude_none=True)
            for criterion in payload.criteria
        ],
        "modelAnswers": list(payload.model_answers),
        "objectives": list(payload.objectives),
    }
    if payload.subject is not None:
        context["subject"] = payload.subject
    return context


async def run(
    payload: EssayGradeIn, *, model: ModelClient, prompts: EssayGradingPrompts, settings: Settings
) -> EssayGradingResult:
    errors = _limit_errors(payload, settings)
    if errors:
        raise ValidationFailedError(errors)
    safe_context = json.dumps(
        strip_fields(_context(payload), DELIMITER_TAG), ensure_ascii=False, separators=(",", ":")
    )
    turn = render(
        prompts.turn.text,
        {"context": safe_context, "essay": strip_tags(payload.essay, DELIMITER_TAG)},
    )
    request = ModelRequest(
        system=prompts.system.text,
        messages=(ModelMessage(role="user", content=turn),),
        max_tokens=settings.essay_grading_max_tokens,
        model=settings.essay_grading_model,
        timeout_seconds=settings.essay_grading_timeout_seconds,
        output_schema=prompts.output_schema,
    )
    started = time.perf_counter()
    reply = await model.complete(request)
    latency_ms = round((time.perf_counter() - started) * 1000)
    parsed = parse_model_grade(reply.text, payload.criteria)
    cost = estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings)
    logger.info(
        "essay_grading.completed",
        pipeline=PIPELINE_NAME,
        prompt_version=prompts.version,
        model=reply.model,
        tokens_in=reply.input_tokens,
        tokens_out=reply.output_tokens,
        latency_ms=latency_ms,
        cost_usd=float(cost),
        stop_reason=reply.stop_reason,
        criteria=len(parsed.criteria),
        essay_chars=len(payload.essay),
        confidence=parsed.confidence,
    )
    return EssayGradingResult(
        criteria=parsed.criteria,
        total_points=sum(grade.points for grade in parsed.criteria),
        max_points=sum(criterion.points for criterion in payload.criteria),
        justification=parsed.justification,
        confidence=parsed.confidence,
        model=reply.model,
        prompt_version=prompts.version,
        input_tokens=reply.input_tokens,
        output_tokens=reply.output_tokens,
        stop_reason=reply.stop_reason,
        cost_usd=cost,
    )
