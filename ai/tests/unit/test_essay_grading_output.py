import json
from collections.abc import Callable
from typing import Any

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.api.essay_grades.schemas import EssayGradeIn, RubricCriterionIn
from elmanhg_ai.core.errors import ModelOutputInvalidError
from elmanhg_ai.pipelines.essay_grading_output import CriterionGrade, parse_model_grade

PayloadBuilder = Callable[..., dict[str, Any]]


@pytest.fixture
def criteria(essay_payload: PayloadBuilder) -> list[RubricCriterionIn]:
    return EssayGradeIn.model_validate(essay_payload()).criteria


def _reply(
    grades: list[tuple[str, int, str]], *, justification: str = "جيد", confidence: float = 0.8
) -> str:
    return json.dumps(
        {
            "criteria": [
                {"criterionId": cid, "justification": why, "points": points}
                for cid, points, why in grades
            ],
            "justification": justification,
            "confidence": confidence,
        },
        ensure_ascii=False,
    )


def test_parse_model_grade_valid_returns_rubric_order(criteria: list[RubricCriterionIn]) -> None:
    parsed = parse_model_grade(_reply([("c2", 3, "مثال واضح"), ("c1", 1, "ناقص")]), criteria)

    assert parsed.criteria == (
        CriterionGrade("c1", 1, "ناقص"),
        CriterionGrade("c2", 3, "مثال واضح"),
    )
    assert (parsed.justification, parsed.confidence) == ("جيد", 0.8)


def test_parse_model_grade_not_json_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade("الدرجة 5 من 5", criteria)


def test_parse_model_grade_missing_criterion_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply([("c1", 1, "ناقص")]), criteria)


def test_parse_model_grade_unknown_criterion_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply([("c1", 1, "ناقص"), ("c9", 3, "زائد")]), criteria)


def test_parse_model_grade_duplicate_criterion_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    grades = [("c1", 1, "ناقص"), ("c1", 2, "كامل"), ("c2", 3, "واضح")]

    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply(grades), criteria)


@pytest.mark.parametrize("points", [-1, 3])
def test_parse_model_grade_points_out_of_range_raises_model_output_invalid(
    criteria: list[RubricCriterionIn], points: int
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply([("c1", points, "ناقص"), ("c2", 3, "واضح")]), criteria)


def test_parse_model_grade_confidence_out_of_range_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    grades = [("c1", 1, "ناقص"), ("c2", 3, "واضح")]

    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply(grades, confidence=1.2), criteria)


def test_parse_model_grade_blank_justification_raises_model_output_invalid(
    criteria: list[RubricCriterionIn],
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply([("c1", 1, "  "), ("c2", 3, "واضح")]), criteria)


def test_parse_model_grade_rejection_logs_reason_without_text(
    criteria: list[RubricCriterionIn], log_capture: LogCapture
) -> None:
    secret = "تبرير سري لا يجب تسجيله"

    with pytest.raises(ModelOutputInvalidError):
        parse_model_grade(_reply([("c1", 9, secret), ("c2", 3, "واضح")]), criteria)

    [entry] = [e for e in log_capture.entries if e["event"] == "essay_grading.output_invalid"]
    assert entry["reason"] == "points"
    assert all(secret not in str(value) for e in log_capture.entries for value in e.values())
