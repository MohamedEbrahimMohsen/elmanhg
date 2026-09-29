from decimal import Decimal

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelReply
from elmanhg_ai.eval.avatar_chat import (
    SAFETY_TAG,
    AvatarEvalCase,
    AvatarEvalReport,
    CaseScore,
    load_cases,
    run,
    score_case,
)
from elmanhg_ai.pipelines import chat
from elmanhg_ai.pipelines.chat import ChatResult, load_chat_prompts
from elmanhg_ai.settings import Settings

STEPS_REPLY = "١. نقسم فرق الجهد على شدة التيار.\n٢. المقاومة = ٤ أوم.\nراجع الشرح."


def _case(case_id: str) -> AvatarEvalCase:
    return next(case for case in load_cases() if case.id == case_id)


def _result(reply: str, citations: tuple[str, ...]) -> ChatResult:
    return ChatResult(
        reply=reply,
        model="claude-sonnet-5",
        prompt_version="v2",
        input_tokens=1,
        output_tokens=1,
        stop_reason="end_turn",
        cost_usd=Decimal("0"),
        citations=citations,
    )


def _reply(text: str, citations: tuple[str, ...]) -> ModelReply:
    return ModelReply(
        text=text,
        model="claude-sonnet-5",
        input_tokens=1,
        output_tokens=1,
        stop_reason="end_turn",
        citations=citations,
    )


def _report(passed: int, failed: int, *, safety_failed: bool) -> AvatarEvalReport:
    passing = [CaseScore(case_id=f"p{i}", tags=(), failures=()) for i in range(passed)]
    tags = (SAFETY_TAG,) if safety_failed else ()
    failing = [CaseScore(case_id=f"f{i}", tags=tags, failures=("length",)) for i in range(failed)]
    return AvatarEvalReport(scores=(*passing, *failing))


def test_load_cases_dataset_has_at_least_20_unique_cases() -> None:
    cases = load_cases()

    assert len(cases) >= 20
    assert len({case.id for case in cases}) == len(cases)


def test_load_cases_dataset_has_at_least_five_safety_cases() -> None:
    assert sum(1 for case in load_cases() if SAFETY_TAG in case.tags) >= 5


def test_load_cases_citation_cases_have_sources() -> None:
    citing = [case for case in load_cases() if case.expect.require_citation]

    assert citing
    assert all(case.request.sources for case in citing)


def test_load_cases_requests_fit_pipeline_limits(settings: Settings) -> None:
    for case in load_cases():
        context_json = case.request.context.model_dump_json(by_alias=True, exclude_none=True)
        assert chat._limit_errors(case.request, context_json, settings) == [], case.id


def test_score_case_all_expectations_met_passes() -> None:
    case = _case("ohm-calc-steps")

    score = score_case(case, _result(STEPS_REPLY, ("explanation-2",)))

    assert score.passed
    assert score.case_id == "ohm-calc-steps"


def test_score_case_reports_each_failed_scorer() -> None:
    case = _case("offcurr-football")
    reply = "The Ahly team will win the league this year " * 10 + "الأهلي"

    score = score_case(case, _result(reply, ("unknown-1",)))

    assert score.failures == ("language", "length", "citations", "includes", "excludes")
    assert not score.passed


async def test_run_with_scripted_fake_reports_pass_rate(settings: Settings) -> None:
    cases = [_case("ohm-calc-steps"), _case("ohm-definition")]
    model = FakeModelClient(
        [_reply(STEPS_REPLY, ("explanation-2",)), _reply("قانون أوم.", ("explanation-1",))]
    )

    report = await run(cases, model=model, prompts=load_chat_prompts("v2"), settings=settings)

    assert report.pass_rate == 0.5
    assert report.failed_ids == ("ohm-definition",)
    assert len(model.requests) == 2


def test_report_safety_failure_misses_threshold_despite_pass_rate() -> None:
    report = _report(19, 1, safety_failed=True)

    assert report.pass_rate == 0.95
    assert report.safety_failures == ("f0",)
    assert not report.meets_threshold()


def test_report_all_pass_meets_threshold() -> None:
    report = _report(22, 0, safety_failed=False)

    assert report.pass_rate == 1.0
    assert report.meets_threshold()
