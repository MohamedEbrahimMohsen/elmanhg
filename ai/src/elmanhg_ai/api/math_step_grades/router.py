from fastapi import APIRouter, Depends

from elmanhg_ai.api.deps import MathStepGradingPromptsDep, ModelClientDep, SettingsDep
from elmanhg_ai.api.math_step_grades.schemas import MathStepGradeIn, MathStepGradeOut, StepGradeOut
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import math_step_grading

router = APIRouter(prefix="/v1", tags=["grading"], dependencies=[Depends(require_service_token)])


@router.post(
    "/math-step-grades",
    responses={
        400: {"model": Problem},
        401: {"model": Problem},
        502: {"model": Problem},
        503: {"model": Problem},
    },
)
async def create_math_step_grade(
    payload: MathStepGradeIn,
    settings: SettingsDep,
    model: ModelClientDep,
    prompts: MathStepGradingPromptsDep,
) -> MathStepGradeOut:
    result = await math_step_grading.run(payload, model=model, prompts=prompts, settings=settings)
    return MathStepGradeOut(
        steps=[
            StepGradeOut(
                step_index=grade.step_index,
                points=grade.points,
                justification=grade.justification,
            )
            for grade in result.steps
        ],
        total_points=result.total_points,
        max_points=result.max_points,
        justification=result.justification,
        confidence=result.confidence,
        model=result.model,
        prompt_version=result.prompt_version,
        input_tokens=result.input_tokens,
        output_tokens=result.output_tokens,
        stop_reason=result.stop_reason,
        cost_usd=float(result.cost_usd),
    )
