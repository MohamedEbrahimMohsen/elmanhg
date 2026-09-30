import time
from typing import Final

import pytest
import sympy

from elmanhg_ai.cas.check import evaluate
from elmanhg_ai.cas.elements import assignment
from elmanhg_ai.cas.models import (
    AnswerForm,
    CasLimits,
    CasRequest,
    MathParseError,
    RelationOperator,
    Verdict,
)
from elmanhg_ai.cas.parser import parse_answer

LIMITS: Final = CasLimits(
    max_tokens=300,
    max_depth=30,
    max_elements=10,
    max_number_digits=30,
    max_exponent=1000,
    max_magnitude=10000,
)
X: Final = sympy.Symbol("x")
TOWER: Final = "((9^{999})^{999})^{999}"


def single(text: str) -> sympy.Expr:
    (element,) = parse_answer(text, LIMITS)
    return element.left


def test_parse_implicit_multiplication_builds_product() -> None:
    value = single("2x(x+1)")

    assert sympy.expand(value) == 2 * X**2 + 2 * X


def test_parse_frac_and_nth_root_evaluate() -> None:
    (half,) = parse_answer("\\frac{6}{4}", LIMITS)

    assert half.left == sympy.Rational(3, 2)
    assert half.has_unreduced_fraction
    assert single("\\sqrt[3]{27}") == 3


def test_parse_tex_single_token_exponent_splits_digits() -> None:
    assert single("x^23") == 3 * X**2


def test_parse_relation_and_assignment_detected() -> None:
    (equation,) = parse_answer("2 = x", LIMITS)
    (inequality,) = parse_answer("x > 3", LIMITS)

    target = assignment(equation)
    assert target is not None
    assert (target[0], target[1]) == (X, 2)
    assert inequality.operator == RelationOperator.GT


def test_parse_chained_relation_raises() -> None:
    with pytest.raises(MathParseError):
        parse_answer("x = 1 = 2", LIMITS)


def test_parse_list_and_plus_minus_expand() -> None:
    roots = parse_answer("x = \\pm 3", LIMITS)
    members = parse_answer("\\{2, -3\\}", LIMITS)

    assert [element.right for element in roots] == [3, -3]
    assert [element.left for element in members] == [2, -3]


@pytest.mark.parametrize(
    "text",
    ["(2, 3)", "\\pm1\\pm2", "x +", "\\frac{1}{", "x^2^3", ""],
    ids=[
        "comma-in-brackets",
        "two-plus-minus",
        "dangling-plus",
        "open-group",
        "double-power",
        "empty",
    ],
)
def test_parse_invalid_structures_raise(text: str) -> None:
    with pytest.raises(MathParseError):
        parse_answer(text, LIMITS)


def test_parse_depth_limit_raises() -> None:
    with pytest.raises(MathParseError):
        parse_answer("(" * 31 + "x" + ")" * 31, LIMITS)


def test_parse_power_tower_raises() -> None:
    with pytest.raises(MathParseError):
        parse_answer("2^{2^{2^{10}}}", LIMITS)


@pytest.mark.parametrize(
    "text",
    [
        "((9^{999})^{999})^{999}",
        "(9^{999})^{999}",
        r"(2^{999}\cdot 3)^{999}",
        "((x+1)^{999})^{999}",
        r"\sqrt[0.001]{\sqrt[0.001]{\sqrt[0.001]{9}}}",
        r"\sqrt[\frac{1}{999}]{\sqrt[\frac{1}{999}]{\sqrt[\frac{1}{999}]{9}}}",
        r"\sqrt[0.001]{9^{999}}",
    ],
    ids=[
        "tower-of-three",
        "tower-of-two",
        "product-base",
        "symbolic-tower",
        "nested-root",
        "fractional-index-root",
        "root-of-power",
    ],
)
def test_parse_compounded_power_raises(text: str) -> None:
    with pytest.raises(MathParseError, match="power too large"):
        parse_answer(text, LIMITS)


@pytest.mark.parametrize("text", [r"\sqrt[0]{9}", r"\sqrt[0.0001]{9}"], ids=["zero", "tiny"])
def test_parse_root_with_zero_or_tiny_index_raises(text: str) -> None:
    with pytest.raises(MathParseError, match="exponent too large"):
        parse_answer(text, LIMITS)


def test_parse_single_large_power_accepted() -> None:
    assert single("9^{999}") == sympy.Integer(9) ** 999


def test_evaluate_compounded_power_is_unreadable_without_evaluation() -> None:
    started = time.perf_counter()

    outcome = evaluate(CasRequest(TOWER, ("1",), AnswerForm.EQUIVALENT, None, LIMITS))

    assert outcome.verdict == Verdict.UNREADABLE
    assert time.perf_counter() - started < 1.0


def test_parse_constants_and_logarithms() -> None:
    assert single("e") == sympy.E
    assert sympy.simplify(single("\\log 100") - 2) == 0
    assert sympy.simplify(single("\\log_2 8") - 3) == 0
    assert single("\\ln e") == 1
    assert single("\\sin^2 x") == sympy.sin(X) ** 2


def test_parse_decimal_sets_flag_and_is_exact() -> None:
    (element,) = parse_answer("0.1", LIMITS)

    assert element.left == sympy.Rational(1, 10)
    assert element.has_decimal
