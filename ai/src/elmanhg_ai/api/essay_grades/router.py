from fastapi import APIRouter, Depends

from elmanhg_ai.api.deps import EssayGradingPromptsDep, ModelClientDep, SettingsDep
from elmanhg_ai.api.essay_grades.schemas import CriterionGradeOut, EssayGradeIn, EssayGradeOut
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import essay_grading

router = APIRouter(prefix="/v1", tags=["grading"], dependencies=[Depends(require_service_token)])


@router.post(
    "/essay-grades",
    responses={
        400: {"model": Problem},
        401: {"model": Problem},
        502: {"model": Problem},
        503: {"model": Problem},
    },
)
async def create_essay_grade(
    payload: EssayGradeIn,
    settings: SettingsDep,
    model: ModelClientDep,
    prompts: EssayGradingPromptsDep,
) -> EssayGradeOut:
    result = await essay_grading.run(payload, model=model, prompts=prompts, settings=settings)
    return EssayGradeOut(
        criteria=[
            CriterionGradeOut(
                criterion_id=grade.criterion_id,
                points=grade.points,
                justification=grade.justification,
            )
            for grade in result.criteria
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
