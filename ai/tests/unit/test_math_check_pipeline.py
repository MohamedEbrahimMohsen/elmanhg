from decimal import Decimal
from typing import Any

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.api.math_checks.schemas import MathCheckIn
from elmanhg_ai.cas.models import CasLimits, CasRequest, CheckOutcome, ToleranceMode, Verdict
from elmanhg_ai.core.errors import ValidationFailedError
from elmanhg_ai.pipelines import math_check
from elmanhg_ai.settings import Settings

SECRET_ANSWER = "x = \\frac{4}{2}"


class RecordingChecker:
    def __init__(self, outcome: CheckOutcome) -> None:
        self.outcome = outcome
        self.requests: list[CasRequest] = []

    async def check(self, request: CasRequest) -> CheckOutcome:
        self.requests.append(request)
        return self.outcome


def payload(**overrides: Any) -> MathCheckIn:
    base: dict[str, Any] = {"answer": SECRET_ANSWER, "expected": ["x = 2"]}
    return MathCheckIn.model_validate(base | overrides)


@pytest.mark.parametrize(
    ("overrides", "field", "code"),
    [
        ({"answer": "x" * 501}, "answer", "TOO_LONG"),
        ({"expected": ["2"] * 21}, "expected", "TOO_MANY_ITEMS"),
        ({"expected": ["x" * 501]}, "expected[0]", "TOO_LONG"),
    ],
    ids=["answer-too-long", "too-many-expected", "expected-too-long"],
)
async def test_run_limits_exceeded_raise_validation_failed(
    settings: Settings, overrides: dict[str, Any], field: str, code: str
) -> None:
    checker = RecordingChecker(CheckOutcome(Verdict.EQUIVALENT, 0, ()))

    with pytest.raises(ValidationFailedError) as error:
        await math_check.run(payload(**overrides), checker=checker, settings=settings)

    assert [(e.field, e.code) for e in error.value.errors] == [(field, code)]
    assert checker.requests == []


async def test_run_passes_request_to_checker_and_returns_result(settings: Settings) -> None:
    checker = RecordingChecker(CheckOutcome(Verdict.WRONG_FORM, None, (1,)))

    result = await math_check.run(
        payload(tolerance="0.01", toleranceMode="absolute", expected=["x = 2", "x +"]),
        checker=checker,
        settings=settings,
    )

    (sent,) = checker.requests
    assert sent.tolerance is not None
    assert (sent.tolerance.value, sent.tolerance.mode) == (Decimal("0.01"), ToleranceMode.ABSOLUTE)
    assert (sent.answer, sent.expected) == (SECRET_ANSWER, ("x = 2", "x +"))
    assert sent.limits == CasLimits.from_settings(settings)
    assert (result.verdict, result.matched_index, result.invalid_expected) == (
        Verdict.WRONG_FORM,
        None,
        (1,),
    )


async def test_run_logs_verdict_without_answer_text(
    settings: Settings, log_capture: LogCapture
) -> None:
    checker = RecordingChecker(CheckOutcome(Verdict.EQUIVALENT, 0, ()))

    await math_check.run(payload(), checker=checker, settings=settings)

    (entry,) = [e for e in log_capture.entries if e["event"] == "math_check.completed"]
    assert (entry["verdict"], entry["answer_chars"]) == ("equivalent", len(SECRET_ANSWER))
    assert "latency_ms" in entry
    logged = " ".join(str(value) for record in log_capture.entries for value in record.values())
    assert SECRET_ANSWER not in logged
    assert "x = 2" not in logged
