from typing import Annotated

from pydantic import Field

from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class MathStepGradeIn(ApiInModel):
    question: str = Field(min_length=1)
    model_solution: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)
    accepted_answers: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)
    steps: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)
    final_answer: str = Field(min_length=1)
    subject: str | None = None
    objectives: list[str] = Field(default_factory=list)


class StepGradeOut(ApiOutModel):
    step_index: int
    points: int
    justification: str


class MathStepGradeOut(ApiOutModel):
    steps: list[StepGradeOut]
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
