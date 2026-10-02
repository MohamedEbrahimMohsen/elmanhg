import pytest
from pydantic import ValidationError

from elmanhg_ai.clients.model import build_model_client
from elmanhg_ai.eval.math_step_grading import load_cases, run
from elmanhg_ai.pipelines.math_step_grading import load_math_step_grading_prompts
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.eval

SKIP_REASON = (
    "set ELMANHG_AI_SERVICE_TOKEN, ELMANHG_AI_LLM_PROVIDER=openai_compatible and "
    "ELMANHG_AI_LLM_API_KEY or ELMANHG_AI_OPENAI_API_KEY to run the math step grading eval"
)


async def test_eval_math_step_grading_v1_meets_threshold() -> None:
    try:
        settings = Settings()
    except ValidationError:
        pytest.skip(SKIP_REASON)
    if settings.llm_provider != "openai_compatible":
        pytest.skip(SKIP_REASON)
    model = build_model_client(settings)
    try:
        report = await run(
            load_cases(),
            model=model,
            prompts=load_math_step_grading_prompts(settings.math_step_grading_prompt_version),
            settings=settings,
        )
    finally:
        await model.aclose()

    assert report.meets_threshold(), (
        f"mean total error {report.mean_total_error:.3f}, "
        f"within one {report.step_within_one_rate:.2f}, "
        f"exact {report.step_exact_rate:.2f}, safety failures {report.safety_failures}"
    )
