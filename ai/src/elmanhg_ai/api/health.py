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
    if getattr(state, "model_client", None) is None or getattr(state, "chat_prompts", None) is None:
        raise ServiceNotReadyError()
    return HealthOut(status="ok")
