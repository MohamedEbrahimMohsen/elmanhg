from typing import Literal

from fastapi import APIRouter, Request

from elmanhg_ai.core.errors import ServiceNotReadyError
from elmanhg_ai.core.models import ApiOutModel


class HealthOut(ApiOutModel):
    status: Literal["ok"]


router = APIRouter(tags=["health"], include_in_schema=False)


@router.get("/health")
async def live() -> HealthOut:
    return HealthOut(status="ok")


@router.get("/health/ready")
async def ready(request: Request) -> HealthOut:
    state = request.app.state
    required = ("model_client", "chat_prompts", "essay_grading_prompts")
    if any(getattr(state, name, None) is None for name in required):
        raise ServiceNotReadyError()
    return HealthOut(status="ok")
