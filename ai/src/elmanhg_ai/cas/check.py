from collections.abc import Sequence
from typing import Final

from elmanhg_ai.cas.elements import ParsedElement
from elmanhg_ai.cas.equivalence import match_answers
from elmanhg_ai.cas.forms import satisfies_form
from elmanhg_ai.cas.models import (
    AnswerForm,
    CasLimits,
    CasRequest,
    CheckOutcome,
    MathParseError,
    Tolerance,
    Verdict,
)
from elmanhg_ai.cas.parser import parse_answer

SYMPY_FAILURES: Final = (
    ArithmeticError,
    ValueError,
    TypeError,
    NotImplementedError,
    RecursionError,
)
Pairs = list[tuple[ParsedElement, ParsedElement]]


def evaluate(request: CasRequest) -> CheckOutcome:
    valid: list[tuple[int, tuple[ParsedElement, ...]]] = []
    invalid: list[int] = []
    for index, text in enumerate(request.expected):
        elements = _parse(text, request.limits)
        if elements is None:
            invalid.append(index)
        else:
            valid.append((index, elements))
    invalid_expected = tuple(invalid)
    if not valid:
        return CheckOutcome(Verdict.UNCHECKED, None, invalid_expected)
    answer = _parse(request.answer, request.limits)
    if answer is None:
        return CheckOutcome(Verdict.UNREADABLE, None, invalid_expected)
    wrong_form = False
    for index, expected in valid:
        pairs = _safe_match(answer, expected, request.tolerance)
        if pairs is None:
            continue
        if _safe_form(pairs, request.form):
            return CheckOutcome(Verdict.EQUIVALENT, index, invalid_expected)
        wrong_form = True
    verdict = Verdict.WRONG_FORM if wrong_form else Verdict.NOT_EQUIVALENT
    return CheckOutcome(verdict, None, invalid_expected)


def _parse(text: str, limits: CasLimits) -> tuple[ParsedElement, ...] | None:
    try:
        return parse_answer(text, limits)
    except (MathParseError, *SYMPY_FAILURES):
        return None


def _safe_match(
    answer: Sequence[ParsedElement], expected: Sequence[ParsedElement], tolerance: Tolerance | None
) -> Pairs | None:
    try:
        return match_answers(answer, expected, tolerance)
    except SYMPY_FAILURES:
        return None


def _safe_form(pairs: Pairs, form: AnswerForm) -> bool:
    try:
        return satisfies_form(pairs, form)
    except SYMPY_FAILURES:
        return False
