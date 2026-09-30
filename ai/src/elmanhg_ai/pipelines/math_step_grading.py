import json
import time
from dataclasses import dataclass
from decimal import Decimal
from typing import Final

import structlog
from pydantic import JsonValue

from elmanhg_ai.api.math_step_grades.schemas import MathStepGradeIn
from elmanhg_ai.clients.model import ModelClient, ModelMessage, ModelRequest, estimate_cost_usd
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.pipelines.math_step_grading_output import (
    MAX_STEP_POINTS,
    StepGrade,
    parse_model_step_grade,
)
from elmanhg_ai.prompts.delimiters import delimiter_pattern, strip_fields
from elmanhg_ai.prompts.loader import Prompt, load_output_schema, load_prompt, render
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "math_step_grading"
SYSTEM_PROMPT: Final = "math_step_grade_system"
TURN_PROMPT: Final = "math_step_grade_turn"
OUTPUT_SCHEMA: Final = "math_step_grade_output"
DELIMITER_TAG: Final = delimiter_pattern("grading_context", "student_work")

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class MathStepGradingPrompts:
    system: Prompt
    turn: Prompt
    output_schema: str

    @property
    def version(self) -> str:
        return self.system.version


@dataclass(frozen=True, slots=True)
class MathStepGradingResult:
    steps: tuple[StepGrade, ...]
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


def load_math_step_grading_prompts(version: str) -> MathStepGradingPrompts:
    return MathStepGradingPrompts(
        system=load_prompt(SYSTEM_PROMPT, version),
        turn=load_prompt(TURN_PROMPT, version),
        output_schema=load_output_schema(OUTPUT_SCHEMA, version),
    )


def _too_long(loc: str, value: str, limit: int) -> list[FieldError]:
    if len(value) <= limit:
        return []
    return [FieldError(loc, "TOO_LONG", f"at most {limit} characters")]


def _too_many(loc: str, values: list[str], limit: int, noun: str) -> list[FieldError]:
    if len(values) <= limit:
        return []
    return [FieldError(loc, "TOO_MANY_ITEMS", f"at most {limit} {noun}")]


def _each_too_long(loc: str, values: list[str], limit: int) -> list[FieldError]:
    return [
        error
        for index, value in enumerate(values)
        for error in _too_long(f"{loc}[{index}]", value, limit)
    ]


def _limit_errors(payload: MathStepGradeIn, settings: Settings) -> list[FieldError]:
    field_limit = settings.math_step_grading_max_field_chars
    step_limit = settings.math_step_grading_max_step_chars
    max_steps = settings.math_step_grading_max_steps
    return [
        *_too_long("question", payload.question, field_limit),
        *_too_many("modelSolution", payload.model_solution, max_steps, "steps"),
        *_each_too_long("modelSolution", payload.model_solution, step_limit),
        *_too_many("steps", payload.steps, max_steps, "steps"),
        *_each_too_long("steps", payload.steps, step_limit),
        *_too_long("finalAnswer", payload.final_answer, step_limit),
        *_too_many(
            "acceptedAnswers",
            payload.accepted_answers,
            settings.math_step_grading_max_accepted_answers,
            "answers",
        ),
        *_each_too_long("acceptedAnswers", payload.accepted_answers, step_limit),
        *_too_many(
            "objectives",
            payload.objectives,
            settings.math_step_grading_max_objectives,
            "objectives",
        ),
        *_each_too_long("objectives", payload.objectives, field_limit),
    ]


def _numbered(steps: list[str]) -> list[JsonValue]:
    return [{"index": index, "step": step} for index, step in enumerate(steps)]


def _context(payload: MathStepGradeIn) -> dict[str, JsonValue]:
    context: dict[str, JsonValue] = {
        "question": payload.question,
        "modelSolution": _numbered(payload.model_solution),
        "acceptedAnswers": list(payload.accepted_answers),
        "objectives": list(payload.objectives),
    }
    if payload.subject is not None:
        context["subject"] = payload.subject
    return context


def _work(payload: MathStepGradeIn) -> dict[str, JsonValue]:
    return {"steps": _numbered(payload.steps), "finalAnswer": payload.final_answer}


def _dump(value: dict[str, JsonValue]) -> str:
    return json.dumps(strip_fields(value, DELIMITER_TAG), ensure_ascii=False, separators=(",", ":"))


async def run(
    payload: MathStepGradeIn,
    *,
    model: ModelClient,
    prompts: MathStepGradingPrompts,
    settings: Settings,
) -> MathStepGradingResult:
    errors = _limit_errors(payload, settings)
    if errors:
        raise ValidationFailedError(errors)
    turn = render(
        prompts.turn.text, {"context": _dump(_context(payload)), "work": _dump(_work(payload))}
    )
    request = ModelRequest(
        system=prompts.system.text,
        messages=(ModelMessage(role="user", content=turn),),
        max_tokens=settings.math_step_grading_max_tokens,
        model=settings.math_step_grading_model,
        timeout_seconds=settings.math_step_grading_timeout_seconds,
        output_schema=prompts.output_schema,
    )
    started = time.perf_counter()
    reply = await model.complete(request)
    latency_ms = round((time.perf_counter() - started) * 1000)
    parsed = parse_model_step_grade(reply.text, len(payload.model_solution))
    cost = estimate_cost_usd(reply.input_tokens, reply.output_tokens, settings)
    logger.info(
        "math_step_grading.completed",
        pipeline=PIPELINE_NAME,
        prompt_version=prompts.version,
        model=reply.model,
        tokens_in=reply.input_tokens,
        tokens_out=reply.output_tokens,
        latency_ms=latency_ms,
        cost_usd=float(cost),
        stop_reason=reply.stop_reason,
        model_steps=len(payload.model_solution),
        student_steps=len(payload.steps),
        confidence=parsed.confidence,
    )
    return MathStepGradingResult(
        steps=parsed.steps,
        total_points=sum(grade.points for grade in parsed.steps),
        max_points=MAX_STEP_POINTS * len(payload.model_solution),
        justification=parsed.justification,
        confidence=parsed.confidence,
        model=reply.model,
        prompt_version=prompts.version,
        input_tokens=reply.input_tokens,
        output_tokens=reply.output_tokens,
        stop_reason=reply.stop_reason,
        cost_usd=cost,
    )
