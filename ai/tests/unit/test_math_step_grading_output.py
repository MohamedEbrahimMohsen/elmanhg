import json

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.core.errors import ModelOutputInvalidError
from elmanhg_ai.pipelines.math_step_grading_output import StepGrade, parse_model_step_grade


def _reply(
    grades: list[tuple[int, int, str]], *, justification: str = "جيد", confidence: float = 0.8
) -> str:
    return json.dumps(
        {
            "steps": [
                {"stepIndex": index, "justification": why, "points": points}
                for index, points, why in grades
            ],
            "justification": justification,
            "confidence": confidence,
        },
        ensure_ascii=False,
    )


def test_parse_model_step_grade_valid_returns_index_order() -> None:
    parsed = parse_model_step_grade(_reply([(1, 1, "ناقصة"), (0, 2, "صحيحة")]), 2)

    assert parsed.steps == (StepGrade(0, 2, "صحيحة"), StepGrade(1, 1, "ناقصة"))
    assert (parsed.justification, parsed.confidence) == ("جيد", 0.8)


def test_parse_model_step_grade_not_json_raises_model_output_invalid() -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade("الدرجة 4 من 4", 2)


def test_parse_model_step_grade_missing_step_raises_model_output_invalid() -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply([(0, 2, "صحيحة")]), 2)


def test_parse_model_step_grade_unknown_index_raises_model_output_invalid() -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply([(0, 2, "صحيحة"), (2, 2, "زائدة")]), 2)


def test_parse_model_step_grade_duplicate_index_raises_model_output_invalid() -> None:
    grades = [(0, 2, "صحيحة"), (0, 1, "ناقصة"), (1, 2, "صحيحة")]

    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply(grades), 2)


@pytest.mark.parametrize("points", [-1, 3])
def test_parse_model_step_grade_points_out_of_range_raises_model_output_invalid(
    points: int,
) -> None:
    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply([(0, points, "صحيحة"), (1, 2, "صحيحة")]), 2)


def test_parse_model_step_grade_confidence_out_of_range_raises_model_output_invalid() -> None:
    grades = [(0, 2, "صحيحة"), (1, 2, "صحيحة")]

    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply(grades, confidence=1.2), 2)


def test_parse_model_step_grade_rejection_logs_reason_without_text(
    log_capture: LogCapture,
) -> None:
    secret = "تبرير سري لا يجب تسجيله"

    with pytest.raises(ModelOutputInvalidError):
        parse_model_step_grade(_reply([(0, 9, secret), (1, 2, "صحيحة")]), 2)

    [entry] = [e for e in log_capture.entries if e["event"] == "math_step_grading.output_invalid"]
    assert entry["reason"] == "points"
    assert all(secret not in str(value) for e in log_capture.entries for value in e.values())
