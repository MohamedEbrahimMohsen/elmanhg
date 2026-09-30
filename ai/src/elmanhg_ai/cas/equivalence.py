import random
from collections.abc import Sequence
from fractions import Fraction
from typing import Final

import sympy

from elmanhg_ai.cas.elements import ParsedElement, assignment
from elmanhg_ai.cas.models import RelationOperator, Tolerance, ToleranceMode

# A fixed seed makes grading repeatable: the same answer always gets the same verdict.
SAMPLE_SEED: Final = 1729
# Agreeing points needed before two symbolic expressions count as equal.
SAMPLE_POINTS: Final = 5
# Points tried before giving up when many land outside the real domain.
SAMPLE_ATTEMPTS: Final = 24
# Working precision for numeric comparison; well above any tolerance an author can set.
DIGITS: Final = 30
# Agreement bound for sampled points, relative to the expected value.
RELATIVE_GAP: Final = sympy.Rational(1, 10**12)
PERCENT: Final = 100
SWAPPED: Final = {
    RelationOperator.GT: RelationOperator.LT,
    RelationOperator.GE: RelationOperator.LE,
}
ORDERED: Final = frozenset({RelationOperator.LT, RelationOperator.LE})


def expressions_equivalent(a: sympy.Expr, b: sympy.Expr, tolerance: Tolerance | None) -> bool:
    if tolerance is not None and not a.free_symbols and not b.free_symbols:
        within = within_tolerance(a, b, tolerance)
        if within is not None:
            return within
    if a - b == 0 or sympy.simplify(a - b) == 0:
        return True
    return numerically_equal(a, b)


def within_tolerance(a: sympy.Expr, b: sympy.Expr, tolerance: Tolerance) -> bool | None:
    allowed = sympy.Rational(Fraction(tolerance.value))
    if a.is_Rational and b.is_Rational:
        bound = abs(b) * allowed / PERCENT if tolerance.mode == ToleranceMode.PERCENT else allowed
        return bool(abs(a - b) <= bound)
    x, y = a.evalf(DIGITS), b.evalf(DIGITS)
    if not _real_finite(x) or not _real_finite(y):
        return None
    bound = abs(y) * allowed / PERCENT if tolerance.mode == ToleranceMode.PERCENT else allowed
    return bool(abs(x - y) <= sympy.N(bound, DIGITS))


def numerically_equal(a: sympy.Expr, b: sympy.Expr) -> bool:
    symbols = sorted(a.free_symbols | b.free_symbols, key=str)
    rng = random.Random(SAMPLE_SEED)  # noqa: S311 - fixed-seed sampling, not security
    needed = SAMPLE_POINTS if symbols else 1
    agreed = 0
    for attempt in range(SAMPLE_ATTEMPTS):
        sign = 1 if attempt % 2 == 0 else -1
        point = {symbol: sign * sympy.Rational(rng.randint(50, 300), 100) for symbol in symbols}
        x, y = a.evalf(DIGITS, subs=point), b.evalf(DIGITS, subs=point)
        if not _real_finite(x) or not _real_finite(y):
            continue
        scale = abs(y) if abs(y) > 1 else sympy.Integer(1)
        if abs(x - y) > RELATIVE_GAP * scale:
            return False
        agreed += 1
        if agreed >= needed:
            return True
    return False


def relations_match(answer: ParsedElement, expected: ParsedElement) -> bool:
    answer_operator, answer_left, answer_right = _normalised(answer)
    expected_operator, expected_left, expected_right = _normalised(expected)
    if answer_operator != expected_operator:
        return False
    expected_difference = expected_left - expected_right
    answer_difference = answer_left - answer_right
    if sympy.simplify(expected_difference) == 0:
        return bool(sympy.simplify(answer_difference) == 0)
    ratio = sympy.simplify(answer_difference / expected_difference)
    if not ratio.is_number or not ratio.is_finite:
        return False
    if expected_operator in ORDERED:
        return bool(ratio.is_positive)
    return bool(ratio.is_zero is False)


def elements_match(
    answer: ParsedElement, expected: ParsedElement, tolerance: Tolerance | None
) -> bool:
    if expected.operator is None:
        value = _answer_value(answer)
        return value is not None and expressions_equivalent(value, expected.left, tolerance)
    target = assignment(expected)
    if target is None:
        return answer.operator is not None and relations_match(answer, expected)
    if answer.operator is None:
        return expressions_equivalent(answer.left, target[1], tolerance)
    given = assignment(answer)
    if given is None or given[0] != target[0]:
        return False
    return expressions_equivalent(given[1], target[1], tolerance)


def match_answers(
    answer: Sequence[ParsedElement], expected: Sequence[ParsedElement], tolerance: Tolerance | None
) -> list[tuple[ParsedElement, ParsedElement]] | None:
    if len(answer) != len(expected):
        return None
    used: set[int] = set()
    pairs: list[tuple[ParsedElement, ParsedElement]] = []
    for element in expected:
        index = next(
            (
                i
                for i, candidate in enumerate(answer)
                if i not in used and elements_match(candidate, element, tolerance)
            ),
            None,
        )
        if index is None:
            return None
        used.add(index)
        pairs.append((answer[index], element))
    return pairs


def _answer_value(answer: ParsedElement) -> sympy.Expr | None:
    if answer.operator is None:
        return answer.left
    given = assignment(answer)
    return None if given is None else given[1]


def _normalised(element: ParsedElement) -> tuple[RelationOperator | None, sympy.Expr, sympy.Expr]:
    right = element.right if element.right is not None else sympy.Integer(0)
    if element.operator in SWAPPED:
        return SWAPPED[element.operator], right, element.left
    return element.operator, element.left, right


def _real_finite(value: sympy.Expr) -> bool:
    return bool(value.is_number and value.is_real and value.is_finite)
