from decimal import Decimal
from typing import Annotated, Self

from pydantic import Field, model_validator

from elmanhg_ai.cas.models import AnswerForm, ToleranceMode, Verdict
from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class MathCheckIn(ApiInModel):
    answer: str = Field(min_length=1)
    expected: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)
    form: AnswerForm = AnswerForm.EQUIVALENT
    tolerance: Decimal | None = Field(default=None, ge=0)
    tolerance_mode: ToleranceMode | None = None

    @model_validator(mode="after")
    def _tolerance_rules(self) -> Self:
        if (self.tolerance is None) != (self.tolerance_mode is None):
            raise ValueError("tolerance and toleranceMode go together")
        if self.tolerance is not None and self.form != AnswerForm.EQUIVALENT:
            raise ValueError("tolerance needs the equivalent form")
        return self


class MathCheckOut(ApiOutModel):
    verdict: Verdict
    matched_index: int | None
    invalid_expected: list[int]
