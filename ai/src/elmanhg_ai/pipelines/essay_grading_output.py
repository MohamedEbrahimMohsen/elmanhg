from collections import Counter
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final, NoReturn

import structlog
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from pydantic.alias_generators import to_camel

from elmanhg_ai.api.essay_grades.schemas import RubricCriterionIn
from elmanhg_ai.core.errors import ModelOutputInvalidError

logger: Final = structlog.stdlib.get_logger(__name__)


class _ModelCriterionGrade(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel, populate_by_name=True, extra="forbid", str_strip_whitespace=True
    )

    criterion_id: str
    justification: str = Field(min_length=1)
    points: int


class _ModelEssayGrade(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_camel, populate_by_name=True, extra="forbid", str_strip_whitespace=True
    )

    criteria: list[_ModelCriterionGrade]
    justification: str = Field(min_length=1)
    confidence: float = Field(ge=0, le=1)


@dataclass(frozen=True, slots=True)
class CriterionGrade:
    criterion_id: str
    points: int
    justification: str


@dataclass(frozen=True, slots=True)
class ParsedGrade:
    criteria: tuple[CriterionGrade, ...]
    justification: str
    confidence: float


def parse_model_grade(text: str, criteria: Sequence[RubricCriterionIn]) -> ParsedGrade:
    try:
        grade = _ModelEssayGrade.model_validate_json(text)
    except ValidationError:
        _reject("schema")
    counts = Counter(item.criterion_id for item in grade.criteria)
    if any(count > 1 for count in counts.values()) or set(counts) != {c.id for c in criteria}:
        _reject("criteria")
    by_id = {item.criterion_id: item for item in grade.criteria}
    if any(not 0 <= by_id[c.id].points <= c.points for c in criteria):
        _reject("points")
    return ParsedGrade(
        criteria=tuple(
            CriterionGrade(
                criterion_id=c.id,
                points=by_id[c.id].points,
                justification=by_id[c.id].justification,
            )
            for c in criteria
        ),
        justification=grade.justification,
        confidence=grade.confidence,
    )


def _reject(reason: str) -> NoReturn:
    logger.warning("essay_grading.output_invalid", reason=reason)
    raise ModelOutputInvalidError()
