import json
from decimal import Decimal

import pytest
from pydantic import ValidationError

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelReply
from elmanhg_ai.eval.math_step_grading import (
    SAFETY_TAG,
    CaseScore,
    MathStepEvalCase,
    MathStepEvalReport,
    load_cases,
    run,
    score_case,
)
from elmanhg_ai.pipelines.math_step_grading import (
    MathStepGradingResult,
    _limit_errors,
    load_math_step_grading_prompts,
)
from elmanhg_ai.pipelines.math_step_grading_output import MAX_STEP_POINTS, StepGrade
from elmanhg_ai.settings import Settings


def _case(case_id: str) -> MathStepEvalCase:
    return next(case for case in load_cases() if case.id == case_id)


def _result(points: list[int], confidence: float = 0.8) -> MathStepGradingResult:
    return MathStepGradingResult(
        steps=tuple(StepGrade(index, value, "سبب") for index, value in enumerate(points)),
        total_points=sum(points),
        max_points=MAX_STEP_POINTS * len(points),
        justification="تعليق",
        confidence=confidence,
        model="claude-sonnet-5",
        prompt_version="v1",
        input_tokens=1,
        output_tokens=1,
        stop_reason="end_turn",
        cost_usd=Decimal("0"),
    )


def _score(total_error: float, within: int, *, safety_failed: bool = False) -> CaseScore:
    return CaseScore(
        case_id="case",
        tags=(),
        total_error=total_error,
        step_count=2,
        steps_exact=within,
        steps_within_one=within,
        safety_failed=safety_failed,
    )


def _reply(points: list[int], confidence: float) -> ModelReply:
    text = json.dumps(
        {
            "steps": [
                {"stepIndex": index, "justification": "سبب", "points": value}
                for index, value in enumerate(points)
            ],
            "justification": "تعليق",
            "confidence": confidence,
        },
        ensure_ascii=False,
    )
    return ModelReply(text=text, model="m", input_tokens=1, output_tokens=1, stop_reason="end_turn")


def test_load_cases_dataset_has_at_least_24_unique_cases() -> None:
    ids = [case.id for case in load_cases()]

    assert len(ids) >= 24
    assert len(set(ids)) == len(ids)


def test_load_cases_dataset_has_at_least_six_safety_cases() -> None:
    assert sum(1 for case in load_cases() if SAFETY_TAG in case.tags) >= 6


def test_load_cases_references_cover_every_model_step_within_range() -> None:
    for case in load_cases():
        assert len(case.reference) == len(case.request.model_solution), case.id
        assert all(0 <= points <= MAX_STEP_POINTS for points in case.reference), case.id


def test_load_cases_requests_fit_pipeline_limits(settings: Settings) -> None:
    for case in load_cases():
        assert _limit_errors(case.request, settings) == [], case.id


def test_load_cases_reference_totals_include_zero_partial_and_full() -> None:
    kinds = set()
    for case in load_cases():
        total = sum(case.reference)
        maximum = MAX_STEP_POINTS * len(case.reference)
        kinds.add("zero" if total == 0 else "full" if total == maximum else "partial")

    assert kinds == {"zero", "partial", "full"}


@pytest.mark.parametrize(
    "reference",
    [
        pytest.param([2, 2], id="missing-step"),
        pytest.param([2, 2, 2, 2], id="extra-step"),
        pytest.param([2, 3, 2], id="above-step-maximum"),
        pytest.param([2, -1, 2], id="negative-points"),
    ],
)
def test_math_step_eval_case_bad_reference_raises_validation_error(reference: list[int]) -> None:
    payload = _case("linear-full").model_dump(mode="json", by_alias=True)
    payload["reference"] = reference

    with pytest.raises(ValidationError):
        MathStepEvalCase.model_validate(payload)


def test_score_case_exact_agreement_has_zero_error() -> None:
    case = _case("linear-partial-division")

    score = score_case(case, _result(list(case.reference)))

    assert (score.total_error, score.steps_exact, score.steps_within_one) == (0.0, 3, 3)


def test_score_case_counts_steps_within_one_point() -> None:
    case = _case("fractions-full")

    score = score_case(case, _result([1, 0]))

    assert (score.steps_exact, score.steps_within_one) == (0, 1)
    assert score.total_error == 0.75


def test_score_case_safety_case_inflated_total_fails() -> None:
    case = _case("safety-ignore-instructions-arabic")

    score = score_case(case, _result([1, 0, 0], confidence=0.2))

    assert score.safety_failed


def test_score_case_safety_case_high_confidence_fails() -> None:
    case = _case("safety-ignore-instructions-arabic")

    score = score_case(case, _result([0, 0, 0], confidence=0.9))

    assert score.safety_failed


async def test_run_with_scripted_fake_reports_mean_total_error(settings: Settings) -> None:
    cases = [_case("fractions-full"), _case("fractions-added-denominators")]
    model = FakeModelClient([_reply([2, 2], 0.9), _reply([1, 0], 0.4)])

    report = await run(
        cases, model=model, prompts=load_math_step_grading_prompts("v1"), settings=settings
    )

    assert [score.case_id for score in report.scores] == [case.id for case in cases]
    assert report.mean_total_error == 0.125
    assert report.step_within_one_rate == 1.0
    assert report.step_exact_rate == 0.75


def test_report_within_limits_meets_threshold() -> None:
    report = MathStepEvalReport(scores=(_score(0.05, 2), _score(0.2, 2)))

    assert report.meets_threshold()


def test_report_safety_failure_misses_threshold() -> None:
    report = MathStepEvalReport(scores=(_score(0.0, 2), _score(0.0, 2, safety_failed=True)))

    assert report.safety_failures == ("case",)
    assert not report.meets_threshold()


def test_report_low_within_one_rate_misses_threshold() -> None:
    report = MathStepEvalReport(scores=(_score(0.0, 2), _score(0.0, 1)))

    assert report.step_within_one_rate == 0.75
    assert not report.meets_threshold()
