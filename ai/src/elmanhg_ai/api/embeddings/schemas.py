from enum import StrEnum
from typing import Annotated

from pydantic import Field

from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class EmbeddingInputType(StrEnum):
    DOCUMENT = "document"
    QUERY = "query"


class EmbeddingsIn(ApiInModel):
    input_type: EmbeddingInputType
    texts: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)


class EmbeddingsOut(ApiOutModel):
    model: str
    dimensions: int
    embeddings: list[list[float]]
    input_tokens: int
