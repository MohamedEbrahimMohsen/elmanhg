from dataclasses import dataclass
from decimal import Decimal
from enum import StrEnum
from typing import Self

from elmanhg_ai.settings import Settings


class AnswerForm(StrEnum):
    EQUIVALENT = "equivalent"
    SIMPLIFIED = "simplified"
    FACTORED = "factored"
    EXPANDED = "expanded"
    EXACT = "exact"


class ToleranceMode(StrEnum):
    ABSOLUTE = "absolute"
    PERCENT = "percent"


class Verdict(StrEnum):
    EQUIVALENT = "equivalent"
    NOT_EQUIVALENT = "notEquivalent"
    WRONG_FORM = "wrongForm"
    UNREADABLE = "unreadable"
    UNCHECKED = "unchecked"


class RelationOperator(StrEnum):
    EQ = "="
    NE = "!="
    LT = "<"
    LE = "<="
    GT = ">"
    GE = ">="


class MathParseError(Exception):
    pass


@dataclass(frozen=True, slots=True)
class Tolerance:
    value: Decimal
    mode: ToleranceMode


@dataclass(frozen=True, slots=True)
class CasLimits:
    max_tokens: int
    max_depth: int
    max_elements: int
    max_number_digits: int
    max_exponent: int
    max_magnitude: int

    @classmethod
    def from_settings(cls, settings: Settings) -> Self:
        return cls(
            max_tokens=settings.cas_max_tokens,
            max_depth=settings.cas_max_depth,
            max_elements=settings.cas_max_elements,
            max_number_digits=settings.cas_max_number_digits,
            max_exponent=settings.cas_max_exponent,
            max_magnitude=settings.cas_max_magnitude,
        )


@dataclass(frozen=True, slots=True)
class CasRequest:
    answer: str
    expected: tuple[str, ...]
    form: AnswerForm
    tolerance: Tolerance | None
    limits: CasLimits


@dataclass(frozen=True, slots=True)
class CheckOutcome:
    verdict: Verdict
    matched_index: int | None
    invalid_expected: tuple[int, ...]
