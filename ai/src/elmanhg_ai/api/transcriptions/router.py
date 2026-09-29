from fastapi import APIRouter, Depends

from elmanhg_ai.api.deps import SettingsDep, TranscriptionClientDep
from elmanhg_ai.api.transcriptions.schemas import TranscriptionIn, TranscriptionOut
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import transcribe

router = APIRouter(
    prefix="/v1", tags=["transcriptions"], dependencies=[Depends(require_service_token)]
)


@router.post(
    "/transcriptions",
    responses={
        400: {"model": Problem},
        401: {"model": Problem},
        502: {"model": Problem},
        503: {"model": Problem},
    },
)
async def create_transcription(
    payload: TranscriptionIn, settings: SettingsDep, client: TranscriptionClientDep
) -> TranscriptionOut:
    result = await transcribe.run(payload, client=client, settings=settings)
    return TranscriptionOut(text=result.text, model=result.model, language=result.language)
