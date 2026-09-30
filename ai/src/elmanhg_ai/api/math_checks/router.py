from fastapi import APIRouter, Depends

from elmanhg_ai.api.deps import CasPoolDep, SettingsDep
from elmanhg_ai.api.math_checks.schemas import MathCheckIn, MathCheckOut
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import math_check

router = APIRouter(prefix="/v1", tags=["grading"], dependencies=[Depends(require_service_token)])


@router.post("/math-checks", responses={400: {"model": Problem}, 401: {"model": Problem}})
async def create_math_check(
    payload: MathCheckIn, settings: SettingsDep, pool: CasPoolDep
) -> MathCheckOut:
    result = await math_check.run(payload, checker=pool, settings=settings)
    return MathCheckOut(
        verdict=result.verdict,
        matched_index=result.matched_index,
        invalid_expected=list(result.invalid_expected),
    )
