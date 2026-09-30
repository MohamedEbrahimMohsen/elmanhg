from decimal import Decimal
from typing import Final, NamedTuple

import pytest

from elmanhg_ai.cas.check import evaluate
from elmanhg_ai.cas.models import (
    AnswerForm,
    CasLimits,
    CasRequest,
    Tolerance,
    ToleranceMode,
    Verdict,
)

LIMITS: Final = CasLimits(
    max_tokens=300,
    max_depth=30,
    max_elements=10,
    max_number_digits=30,
    max_exponent=1000,
    max_magnitude=10000,
    max_expansion_terms=500,
)
EQ: Final = Verdict.EQUIVALENT
NOT: Final = Verdict.NOT_EQUIVALENT
FORM: Final = Verdict.WRONG_FORM
UNREADABLE: Final = Verdict.UNREADABLE
ARABIC_TWO: Final = chr(0x0662)


def absolute(value: str) -> Tolerance:
    return Tolerance(Decimal(value), ToleranceMode.ABSOLUTE)


class Case(NamedTuple):
    answer: str
    expected: tuple[str, ...]
    verdict: Verdict
    form: AnswerForm = AnswerForm.EQUIVALENT
    tolerance: Tolerance | None = None
    matched: int | None = 0
    invalid: tuple[int, ...] = ()


CASES: Final = {
    "commuted-sum": Case("2x+3", ("3+2x",), EQ),
    "decimal-half": Case("0.5", ("\\frac{1}{2}",), EQ),
    "cancelled-fraction": Case("\\frac{x^2-1}{x-1}", ("x+1",), EQ),
    "surd": Case("2\\sqrt{2}", ("\\sqrt{8}",), EQ),
    "square-expanded": Case("(x+1)^2", ("x^2+2x+1",), EQ),
    "assignment-swapped": Case("x = 2", ("2 = x",), EQ),
    "value-for-assignment": Case("2", ("x = 2",), EQ),
    "pythagorean-identity": Case("\\sin^2 x + \\cos^2 x", ("1",), EQ),
    "unordered-list": Case("2, -3", ("-3, 2",), EQ),
    "plus-minus": Case("x = \\pm 3", ("x = 3, x = -3",), EQ),
    "log-base": Case("\\log_{2} 8", ("3",), EQ),
    "flipped-inequality": Case("x > 3", ("3 < x",), EQ),
    "scaled-equation": Case("2x + 4 = 0", ("x + 2 = 0",), EQ),
    "arabic-digit": Case(f"{ARABIC_TWO}x", ("2x",), EQ),
    "times-sign": Case("x \\times y", ("xy",), EQ),
    "unreduced-fraction": Case("\\frac{6}{4}", ("\\frac{3}{2}",), EQ),
    "pi-commuted": Case("\\pi r^2", ("r^2\\pi",), EQ),
    "cube-root": Case("\\sqrt[3]{27}", ("3",), EQ),
    "unit-text": Case("2 \\text{ سم}", ("2",), EQ),
    "left-right": Case("\\left(x+1\\right)(x-1)", ("x^2-1",), EQ),
    "exp-log": Case("e^{\\ln 5}", ("5",), EQ),
    "set-braces": Case("\\{2, -3\\}", ("2, -3",), EQ),
    "wrong-value": Case("x = 3", ("x = 2",), NOT, matched=None),
    "not-isolated": Case("2x = 4", ("x = 2",), NOT, matched=None),
    "wrong-symbol": Case("y = 2", ("x = 2",), NOT, matched=None),
    "wrong-inequality": Case("x \\ge 3", ("x > 3",), NOT, matched=None),
    "wrong-root": Case("2, 3", ("2, -3",), NOT, matched=None),
    "missing-root": Case("2", ("2, 3",), NOT, matched=None),
    "absolute-value": Case("\\sqrt{x^2}", ("x",), NOT, matched=None),
    "rounded-no-tolerance": Case("1.41", ("\\sqrt{2}",), NOT, matched=None),
    "rounded-within-tolerance": Case("1.41", ("\\sqrt{2}",), EQ, tolerance=absolute("0.01")),
    "outside-tolerance": Case("1.3", ("\\sqrt{2}",), NOT, tolerance=absolute("0.01"), matched=None),
    "percent-inclusive": Case(
        "9.9", ("10",), EQ, tolerance=Tolerance(Decimal(1), ToleranceMode.PERCENT)
    ),
    "assignment-tolerance": Case("x = 1.414", ("x = \\sqrt{2}",), EQ, tolerance=absolute("0.001")),
    "factored-expected": Case(
        "x^2 + 2x + 1", ("(x+1)^2",), FORM, form=AnswerForm.FACTORED, matched=None
    ),
    "factored-repeated": Case("(x+1)(x+1)", ("(x+1)^2",), EQ, form=AnswerForm.FACTORED),
    "factored-product": Case("x(x+1)", ("x^2+x",), EQ, form=AnswerForm.FACTORED),
    "expanded-expected": Case("2(x+1)", ("2x+2",), FORM, form=AnswerForm.EXPANDED, matched=None),
    "exact-expected": Case("0.5", ("\\frac{1}{2}",), FORM, form=AnswerForm.EXACT, matched=None),
    "simplified-fraction": Case(
        "\\frac{6}{4}", ("\\frac{3}{2}",), FORM, form=AnswerForm.SIMPLIFIED, matched=None
    ),
    "simplified-sum": Case("2+3", ("5",), FORM, form=AnswerForm.SIMPLIFIED, matched=None),
    "simplified-ok": Case("\\frac{3}{2}", ("\\frac{3}{2}",), EQ, form=AnswerForm.SIMPLIFIED),
    "dangling-operator": Case("x +", ("2",), UNREADABLE, matched=None),
    "unknown-command": Case("\\input{/etc/passwd}", ("2",), UNREADABLE, matched=None),
    "python-injection": Case("__import__('os')", ("2",), UNREADABLE, matched=None),
    "power-tower": Case("2^{2^{2^{10}}}", ("2",), UNREADABLE, matched=None),
    "long-number": Case("1" * 31, ("2",), UNREADABLE, matched=None),
    "no-valid-expected": Case("2", ("x +",), Verdict.UNCHECKED, matched=None, invalid=(0,)),
    "second-expected": Case("2", ("x +", "2"), EQ, matched=1, invalid=(0,)),
}


@pytest.mark.parametrize("case", CASES.values(), ids=CASES.keys())
def test_evaluate_cases_return_expected_verdict(case: Case) -> None:
    request = CasRequest(case.answer, case.expected, case.form, case.tolerance, LIMITS)

    outcome = evaluate(request)

    assert (outcome.verdict, outcome.matched_index, outcome.invalid_expected) == (
        case.verdict,
        case.matched,
        case.invalid,
    )
