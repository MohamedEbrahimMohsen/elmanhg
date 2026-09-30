from typing import Any

import pytest
from pydantic import ValidationError

from elmanhg_ai.api.math_step_grades.schemas import MathStepGradeIn


def _payload(**overrides: Any) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "question": "حل المعادلة 2x + 3 = 7",
        "modelSolution": ["2x = 4", "x = 2"],
        "acceptedAnswers": ["x = 2"],
        "steps": ["2x = 7 - 3", "x = 2"],
        "finalAnswer": "x = 2",
        "subject": "الرياضيات",
    }
    return payload | overrides


def _loc(payload: dict[str, Any]) -> tuple[int | str, ...]:
    with pytest.raises(ValidationError) as error:
        MathStepGradeIn.model_validate(payload)
    return error.value.errors()[0]["loc"]


def test_math_step_grade_in_valid_payload_parses() -> None:
    grade = MathStepGradeIn.model_validate(_payload())

    assert grade.model_solution == ["2x = 4", "x = 2"]
    assert grade.accepted_answers == ["x = 2"]
    assert grade.final_answer == "x = 2"
    assert grade.objectives == []


def test_math_step_grade_in_empty_model_solution_rejected() -> None:
    assert _loc(_payload(modelSolution=[])) == ("modelSolution",)


def test_math_step_grade_in_blank_step_rejected() -> None:
    assert _loc(_payload(steps=["", "x = 2"])) == ("steps", 0)


def test_math_step_grade_in_blank_final_answer_rejected() -> None:
    assert _loc(_payload(finalAnswer="")) == ("finalAnswer",)


def test_math_step_grade_in_empty_accepted_answers_rejected() -> None:
    assert _loc(_payload(acceptedAnswers=[])) == ("acceptedAnswers",)


def test_math_step_grade_in_extra_field_rejected() -> None:
    with pytest.raises(ValidationError) as error:
        MathStepGradeIn.model_validate(_payload(verdict="equivalent"))

    assert error.value.errors()[0]["type"] == "extra_forbidden"
