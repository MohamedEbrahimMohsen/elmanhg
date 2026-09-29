from fastapi import APIRouter, Depends

from elmanhg_ai.api.chat.schemas import ChatIn, ChatOut
from elmanhg_ai.api.deps import ChatPromptsDep, ModelClientDep, SettingsDep
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import chat

router = APIRouter(prefix="/v1", tags=["chat"], dependencies=[Depends(require_service_token)])


@router.post(
    "/chat",
    responses={
        400: {"model": Problem},
        401: {"model": Problem},
        502: {"model": Problem},
        503: {"model": Problem},
    },
)
async def create_chat_reply(
    payload: ChatIn, settings: SettingsDep, model: ModelClientDep, prompts: ChatPromptsDep
) -> ChatOut:
    result = await chat.run(payload, model=model, prompts=prompts, settings=settings)
    return ChatOut(
        reply=result.reply,
        model=result.model,
        prompt_version=result.prompt_version,
        input_tokens=result.input_tokens,
        output_tokens=result.output_tokens,
        stop_reason=result.stop_reason,
    )
