from typing import Annotated, Self

from pydantic import Field, model_validator

from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class RubricLevelIn(ApiInModel):
    points: int = Field(ge=0)
    description: str = Field(min_length=1)


class RubricCriterionIn(ApiInModel):
    id: str = Field(pattern=r"^[a-z0-9-]{1,20}$")
    title: str = Field(min_length=1)
    description: str | None = None
    points: int = Field(ge=1)
    levels: list[RubricLevelIn] = Field(min_length=2)

    @model_validator(mode="after")
    def _levels_within_points(self) -> Self:
        if any(level.points > self.points for level in self.levels):
            raise ValueError("level points must be between 0 and the criterion points")
        return self


class EssayGradeIn(ApiInModel):
    question: str = Field(min_length=1)
    criteria: list[RubricCriterionIn] = Field(min_length=1)
    model_answers: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)
    essay: str = Field(min_length=1)
    subject: str | None = None
    objectives: list[str] = Field(default_factory=list)

    @model_validator(mode="after")
    def _criterion_ids_unique(self) -> Self:
        ids = [criterion.id for criterion in self.criteria]
        if len(set(ids)) != len(ids):
            raise ValueError("criterion ids must be unique")
        return self


class CriterionGradeOut(ApiOutModel):
    criterion_id: str
    points: int
    justification: str


class EssayGradeOut(ApiOutModel):
    criteria: list[CriterionGradeOut]
    total_points: int
    max_points: int
    justification: str
    confidence: float
    model: str
    prompt_version: str
    input_tokens: int
    output_tokens: int
    stop_reason: str | None
    cost_usd: float
