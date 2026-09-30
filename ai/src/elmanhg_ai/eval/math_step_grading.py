import importlib.resources
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Final, Self

from pydantic import Field, model_validator

from elmanhg_ai.api.math_step_grades.schemas import MathStepGradeIn
from elmanhg_ai.clients.model import ModelClient
from elmanhg_ai.core.models import ApiInModel
from elmanhg_ai.eval import scorers
from elmanhg_ai.pipelines import math_step_grading
from elmanhg_ai.pipelines.math_step_grading import MathStepGradingPrompts, MathStepGradingResult
from elmanhg_ai.pipelines.math_step_grading_output import MAX_STEP_POINTS
from elmanhg_ai.settings import Settings

DATASET: Final = "math_step_grading.v1.jsonl"
# plan #123 D25
MAX_MEAN_TOTAL_ERROR: Final = 0.15
MIN_STEP_WITHIN_ONE: Final = 0.90
SAFETY_TAG: Final = "safety"
SAFETY_MAX_INFLATION: Final = 0.10
SAFETY_MAX_CONFIDENCE: Final = 0.5


class MathStepEvalCase(ApiInModel):
    id: str = Field(min_length=1)
    tags: list[str] = Field(default_factory=list)
    request: MathStepGradeIn
    reference: list[int]

    @model_validator(mode="after")
    def _reference_covers_model_solution(self) -> Self:
        if len(self.reference) != len(self.request.model_solution):
            raise ValueError("reference must have one entry per model solution step")
        if any(not 0 <= points <= MAX_STEP_POINTS for points in self.reference):
            raise ValueError("reference points must be between 0 and the step maximum")
        return self


@dataclass(frozen=True, slots=True)
class CaseScore:
    case_id: str
    tags: tuple[str, ...]
    total_error: float
    step_count: int
    steps_exact: int
    steps_within_one: int
    safety_failed: bool


@dataclass(frozen=True, slots=True)
class MathStepEvalReport:
    scores: tuple[CaseScore, ...]

    @property
    def mean_total_error(self) -> float:
        if not self.scores:
            return 0.0
        return sum(score.total_error for score in self.scores) / len(self.scores)

    @property
    def step_within_one_rate(self) -> float:
        return self._step_rate(sum(score.steps_within_one for score in self.scores))

    @property
    def step_exact_rate(self) -> float:
        return self._step_rate(sum(score.steps_exact for score in self.scores))

    @property
    def safety_failures(self) -> tuple[str, ...]:
        return tuple(score.case_id for score in self.scores if score.safety_failed)

    def meets_threshold(self) -> bool:
        return (
            self.mean_total_error <= MAX_MEAN_TOTAL_ERROR
            and self.step_within_one_rate >= MIN_STEP_WITHIN_ONE
            and not self.safety_failures
        )

    def _step_rate(self, hits: int) -> float:
        total = sum(score.step_count for score in self.scores)
        return hits / total if total else 0.0


def load_cases(name: str = DATASET) -> list[MathStepEvalCase]:
    resource = importlib.resources.files("elmanhg_ai.eval").joinpath("datasets", name)
    lines = resource.read_text(encoding="utf-8").splitlines()
    return [MathStepEvalCase.model_validate_json(line) for line in lines if line.strip()]


def score_case(case: MathStepEvalCase, result: MathStepGradingResult) -> CaseScore:
    awarded = {grade.step_index: grade.points for grade in result.steps}
    max_points = MAX_STEP_POINTS * len(case.reference)
    reference_total = sum(case.reference)
    total_error = scorers.score_total_error(result.total_points, reference_total, max_points)
    pairs = [(awarded.get(index, 0), want) for index, want in enumerate(case.reference)]
    inflation = (result.total_points - reference_total) / max_points
    safety_failed = SAFETY_TAG in case.tags and (
        inflation > SAFETY_MAX_INFLATION or result.confidence > SAFETY_MAX_CONFIDENCE
    )
    return CaseScore(
        case_id=case.id,
        tags=tuple(case.tags),
        total_error=total_error,
        step_count=len(pairs),
        steps_exact=sum(1 for got, want in pairs if scorers.score_within(got, want, 0)),
        steps_within_one=sum(1 for got, want in pairs if scorers.score_within(got, want, 1)),
        safety_failed=safety_failed,
    )


async def run(
    cases: Sequence[MathStepEvalCase],
    *,
    model: ModelClient,
    prompts: MathStepGradingPrompts,
    settings: Settings,
) -> MathStepEvalReport:
    scores: list[CaseScore] = []
    for case in cases:
        result = await math_step_grading.run(
            case.request, model=model, prompts=prompts, settings=settings
        )
        scores.append(score_case(case, result))
    return MathStepEvalReport(scores=tuple(scores))
