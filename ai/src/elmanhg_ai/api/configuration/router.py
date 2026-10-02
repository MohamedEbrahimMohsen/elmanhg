from fastapi import APIRouter, Depends
from pydantic import SecretStr

from elmanhg_ai.api.configuration.schemas import ConfigurationOut, SecretStatusOut
from elmanhg_ai.api.deps import SettingsDep
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.settings import PLACEHOLDER_PREFIX

router = APIRouter(
    prefix="/v1", tags=["configuration"], dependencies=[Depends(require_service_token)]
)


@router.get("/configuration", responses={401: {"model": Problem}})
async def get_configuration(settings: SettingsDep) -> ConfigurationOut:
    return ConfigurationOut(
        llm_provider=settings.llm_provider,
        chat_model=settings.chat_model,
        essay_grading_model=settings.essay_grading_model,
        math_step_grading_model=settings.math_step_grading_model,
        embedding_provider=settings.embedding_provider,
        embedding_model=settings.embedding_model,
        transcription_provider=settings.transcription_provider,
        transcription_model=settings.transcription_model,
        secrets=[
            SecretStatusOut(key="ELMANHG_AI_LLM_API_KEY", is_set=_is_set(settings.llm_api_key)),
            SecretStatusOut(
                key="ELMANHG_AI_OPENAI_API_KEY", is_set=_is_set(settings.openai_api_key)
            ),
        ],
    )


def _is_set(secret: SecretStr | None) -> bool:
    value = secret.get_secret_value().strip() if secret is not None else ""
    return bool(value) and not value.lower().startswith(PLACEHOLDER_PREFIX)
