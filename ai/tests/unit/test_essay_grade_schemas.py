from collections.abc import Callable
from typing import Any

import pytest
from pydantic import ValidationError

from elmanhg_ai.api.essay_grades.schemas import EssayGradeIn

PayloadBuilder = Callable[..., dict[str, Any]]


def _criteria(essay_payload: PayloadBuilder) -> list[dict[str, Any]]:
    criteria: list[dict[str, Any]] = essay_payload()["criteria"]
    return criteria


def test_essay_grade_in_valid_payload_parses(essay_payload: PayloadBuilder) -> None:
    grade = EssayGradeIn.model_validate(essay_payload())

    assert [criterion.id for criterion in grade.criteria] == ["c1", "c2"]
    assert grade.model_answers[0].startswith("القصور الذاتي")
    assert grade.subject == "الفيزياء"


def test_essay_grade_in_criterion_id_invalid_rejected(essay_payload: PayloadBuilder) -> None:
    criteria = _criteria(essay_payload)
    criteria[0] = criteria[0] | {"id": "C 1"}

    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(criteria=criteria))

    assert error.value.errors()[0]["loc"] == ("criteria", 0, "id")


def test_essay_grade_in_duplicate_criterion_ids_rejected(essay_payload: PayloadBuilder) -> None:
    criteria = _criteria(essay_payload)
    criteria[1] = criteria[1] | {"id": "c1"}

    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(criteria=criteria))

    assert "criterion ids must be unique" in str(error.value)


def test_essay_grade_in_level_above_criterion_points_rejected(
    essay_payload: PayloadBuilder,
) -> None:
    criteria = _criteria(essay_payload)
    criteria[0] = criteria[0] | {"points": 1}

    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(criteria=criteria))

    assert "level points must be between 0 and the criterion points" in str(error.value)


def test_essay_grade_in_single_level_rejected(essay_payload: PayloadBuilder) -> None:
    criteria = _criteria(essay_payload)
    criteria[0] = criteria[0] | {"levels": [{"points": 2, "description": "كامل"}]}

    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(criteria=criteria))

    assert error.value.errors()[0]["loc"] == ("criteria", 0, "levels")


def test_essay_grade_in_empty_model_answers_rejected(essay_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(modelAnswers=[]))

    assert error.value.errors()[0]["loc"] == ("modelAnswers",)


def test_essay_grade_in_blank_essay_rejected(essay_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(essay=""))

    assert error.value.errors()[0]["loc"] == ("essay",)


def test_essay_grade_in_extra_field_rejected(essay_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        EssayGradeIn.model_validate(essay_payload(studentId="s-1"))

    assert error.value.errors()[0]["type"] == "extra_forbidden"
