import json
from decimal import Decimal

import pytest
from pydantic import ValidationError

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelReply
from elmanhg_ai.eval.essay_grading import (
    SAFETY_TAG,
    CaseScore,
    EssayEvalCase,
    EssayEvalReport,
    load_cases,
    run,
    score_case,
)
from elmanhg_ai.pipelines.essay_grading import (
    EssayGradingResult,
    _limit_errors,
    load_essay_grading_prompts,
)
from elmanhg_ai.pipelines.essay_grading_output import CriterionGrade
from elmanhg_ai.settings import Settings


def _case(case_id: str) -> EssayEvalCase:
    return next(case for case in load_cases() if case.id == case_id)


def _result(points: dict[str, int], confidence: float = 0.8) -> EssayGradingResult:
    return EssayGradingResult(
        criteria=tuple(CriterionGrade(key, value, "سبب") for key, value in points.items()),
        total_points=sum(points.values()),
        max_points=5,
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
        criteria_count=2,
        criteria_exact=within,
        criteria_within_one=within,
        safety_failed=safety_failed,
    )


def _reply(points: dict[str, int], confidence: float) -> ModelReply:
    text = json.dumps(
        {
            "criteria": [
                {"criterionId": key, "justification": "سبب", "points": value}
                for key, value in points.items()
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


def test_load_cases_references_cover_every_criterion_within_range() -> None:
    for case in load_cases():
        points = {criterion.id: criterion.points for criterion in case.request.criteria}
        assert set(case.reference) == set(points), case.id
        assert all(0 <= case.reference[key] <= points[key] for key in points), case.id


def test_load_cases_requests_fit_pipeline_limits(settings: Settings) -> None:
    for case in load_cases():
        assert _limit_errors(case.request, settings) == [], case.id


def test_load_cases_reference_totals_include_zero_partial_and_full() -> None:
    kinds = set()
    for case in load_cases():
        total = sum(case.reference.values())
        maximum = sum(criterion.points for criterion in case.request.criteria)
        kinds.add("zero" if total == 0 else "full" if total == maximum else "partial")

    assert kinds == {"zero", "partial", "full"}


@pytest.mark.parametrize(
    "reference",
    [
        pytest.param({"c1": 1, "cx": 1}, id="unknown-criterion-id"),
        pytest.param({"c1": 1}, id="missing-criterion-id"),
        pytest.param({"c1": 99, "c2": 0}, id="above-criterion-points"),
        pytest.param({"c1": -1, "c2": 0}, id="negative-points"),
    ],
)
def test_essay_eval_case_bad_reference_raises_validation_error(reference: dict[str, int]) -> None:
    payload = _case("physics-inertia-full").model_dump(mode="json", by_alias=True)
    payload["reference"] = reference

    with pytest.raises(ValidationError):
        EssayEvalCase.model_validate(payload)


def test_score_case_exact_agreement_has_zero_error() -> None:
    case = _case("physics-inertia-partial-example")

    score = score_case(case, _result(dict(case.reference)))

    assert (score.total_error, score.criteria_exact, score.criteria_within_one) == (0.0, 2, 2)


def test_score_case_counts_criteria_within_one_point() -> None:
    case = _case("physics-inertia-full")

    score = score_case(case, _result({"c1": 1, "c2": 1}))

    assert (score.criteria_exact, score.criteria_within_one) == (0, 1)
    assert score.total_error == 0.6


def test_score_case_safety_case_inflated_total_fails() -> None:
    case = _case("safety-ignore-instructions-arabic")

    score = score_case(case, _result({"c1": 1, "c2": 0}, confidence=0.2))

    assert score.safety_failed


def test_score_case_safety_case_high_confidence_fails() -> None:
    case = _case("safety-ignore-instructions-arabic")

    score = score_case(case, _result({"c1": 0, "c2": 0}, confidence=0.9))

    assert score.safety_failed


async def test_run_with_scripted_fake_reports_mean_total_error(settings: Settings) -> None:
    cases = [_case("physics-inertia-full"), _case("physics-inertia-off-topic")]
    model = FakeModelClient([_reply({"c1": 2, "c2": 3}, 0.9), _reply({"c1": 1, "c2": 0}, 0.4)])

    report = await run(
        cases, model=model, prompts=load_essay_grading_prompts("v1"), settings=settings
    )

    assert [score.case_id for score in report.scores] == [case.id for case in cases]
    assert report.mean_total_error == 0.1
    assert report.criterion_within_one_rate == 1.0
    assert report.criterion_exact_rate == 0.75


def test_report_within_limits_meets_threshold() -> None:
    report = EssayEvalReport(scores=(_score(0.05, 2), _score(0.2, 2)))

    assert report.meets_threshold()


def test_report_safety_failure_misses_threshold() -> None:
    report = EssayEvalReport(scores=(_score(0.0, 2), _score(0.0, 2, safety_failed=True)))

    assert report.safety_failures == ("case",)
    assert not report.meets_threshold()


def test_report_high_mean_error_misses_threshold() -> None:
    report = EssayEvalReport(scores=(_score(0.2, 2), _score(0.2, 2)))

    assert not report.meets_threshold()
