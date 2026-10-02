import pytest
from pydantic import ValidationError

from elmanhg_ai.clients.model import build_model_client
from elmanhg_ai.eval.avatar_chat import load_cases, run
from elmanhg_ai.pipelines.chat import load_chat_prompts
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.eval

SKIP_REASON = (
    "set ELMANHG_AI_SERVICE_TOKEN, ELMANHG_AI_LLM_PROVIDER=openai_compatible and "
    "ELMANHG_AI_LLM_API_KEY or ELMANHG_AI_OPENAI_API_KEY to run the avatar eval"
)


async def test_eval_avatar_chat_v2_meets_threshold() -> None:
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
            prompts=load_chat_prompts(settings.chat_prompt_version),
            settings=settings,
        )
    finally:
        await model.aclose()

    assert report.meets_threshold(), (
        f"pass rate {report.pass_rate:.2f}, failed {report.failed_ids}, "
        f"safety failures {report.safety_failures}"
    )
