from decimal import Decimal
from typing import Any

import pytest
from pydantic import ValidationError

from elmanhg_ai.api.math_checks.schemas import MathCheckIn
from elmanhg_ai.cas.models import AnswerForm, ToleranceMode


def payload(**overrides: Any) -> dict[str, Any]:
    base: dict[str, Any] = {"answer": "x=\\frac{4}{2}", "expected": ["x = 2"]}
    return base | overrides


def test_math_check_in_valid_payload_parses() -> None:
    check = MathCheckIn.model_validate(payload(tolerance="0.01", toleranceMode="absolute"))

    assert check.form == AnswerForm.EQUIVALENT
    assert (check.tolerance, check.tolerance_mode) == (Decimal("0.01"), ToleranceMode.ABSOLUTE)
    assert check.expected == ["x = 2"]


@pytest.mark.parametrize(
    ("overrides", "loc"),
    [
        ({"answer": ""}, ("answer",)),
        ({"expected": []}, ("expected",)),
        ({"expected": [""]}, ("expected", 0)),
        ({"tolerance": -1, "toleranceMode": "absolute"}, ("tolerance",)),
        ({"studentId": "s-1"}, ("studentId",)),
        ({"tolerance": 1}, ()),
        ({"toleranceMode": "percent"}, ()),
        ({"tolerance": 1, "toleranceMode": "absolute", "form": "factored"}, ()),
    ],
    ids=[
        "empty-answer",
        "no-expected",
        "blank-expected",
        "negative-tolerance",
        "extra-field",
        "tolerance-without-mode",
        "mode-without-tolerance",
        "tolerance-with-form",
    ],
)
def test_math_check_in_invalid_payload_reports_loc(
    overrides: dict[str, Any], loc: tuple[str | int, ...]
) -> None:
    with pytest.raises(ValidationError) as error:
        MathCheckIn.model_validate(payload(**overrides))

    assert error.value.errors()[0]["loc"] == loc
