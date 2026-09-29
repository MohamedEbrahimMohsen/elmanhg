from fastapi import APIRouter, Depends

from elmanhg_ai.api.deps import EmbeddingClientDep, SettingsDep
from elmanhg_ai.api.embeddings.schemas import EmbeddingsIn, EmbeddingsOut
from elmanhg_ai.core.auth import require_service_token
from elmanhg_ai.core.problems import Problem
from elmanhg_ai.pipelines import embed

router = APIRouter(prefix="/v1", tags=["embeddings"], dependencies=[Depends(require_service_token)])


@router.post(
    "/embeddings",
    responses={
        400: {"model": Problem},
        401: {"model": Problem},
        502: {"model": Problem},
        503: {"model": Problem},
    },
)
async def create_embeddings(
    payload: EmbeddingsIn, settings: SettingsDep, client: EmbeddingClientDep
) -> EmbeddingsOut:
    result = await embed.run(payload, client=client, settings=settings)
    return EmbeddingsOut(
        model=result.model,
        dimensions=result.dimensions,
        embeddings=[list(vector) for vector in result.vectors],
        input_tokens=result.input_tokens,
    )
