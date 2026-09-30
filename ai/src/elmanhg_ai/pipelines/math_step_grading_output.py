from collections import Counter
from dataclasses import dataclass
from typing import Final, NoReturn

import structlog
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from pydantic.alias_generators import to_camel

from elmanhg_ai.core.errors import ModelOutputInvalidError

# The none/partial/full scale per model step, shared with the .NET MathStepsGrader.
MAX_STEP_POINTS: Final = 2

logger: Final = structlog.stdlib.get_logger(__name__)


class _ModelStepGrade(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel, populate_by_name=True, extra="forbid", str_strip_whitespace=True
    )

    step_index: int
    justification: str = Field(min_length=1)
    points: int


class _ModelMathStepGrade(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel, populate_by_name=True, extra="forbid", str_strip_whitespace=True
    )

    steps: list[_ModelStepGrade]
    justification: str = Field(min_length=1)
    confidence: float = Field(ge=0, le=1)


@dataclass(frozen=True, slots=True)
class StepGrade:
    step_index: int
    points: int
    justification: str


@dataclass(frozen=True, slots=True)
class ParsedStepGrade:
    steps: tuple[StepGrade, ...]
    justification: str
    confidence: float


def parse_model_step_grade(text: str, step_count: int) -> ParsedStepGrade:
    try:
        grade = _ModelMathStepGrade.model_validate_json(text)
    except ValidationError:
        _reject("schema")
    counts = Counter(item.step_index for item in grade.steps)
    if any(count > 1 for count in counts.values()) or set(counts) != set(range(step_count)):
        _reject("steps")
    if any(not 0 <= item.points <= MAX_STEP_POINTS for item in grade.steps):
        _reject("points")
    return ParsedStepGrade(
        steps=tuple(
            StepGrade(
                step_index=item.step_index, points=item.points, justification=item.justification
            )
            for item in sorted(grade.steps, key=lambda step: step.step_index)
        ),
        justification=grade.justification,
        confidence=grade.confidence,
    )


def _reject(reason: str) -> NoReturn:
    logger.warning("math_step_grading.output_invalid", reason=reason)
    raise ModelOutputInvalidError()
