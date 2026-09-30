from collections.abc import Callable, Sequence
from math import gcd
from typing import Final

import sympy

from elmanhg_ai.cas.models import CasLimits, MathParseError

# Significant digits used to size a numeric exponent before it is built.
EXPONENT_DIGITS: Final = 15

FUNCTIONS: Final[dict[str, Callable[..., sympy.Expr]]] = {
    "sin": sympy.sin,
    "cos": sympy.cos,
    "tan": sympy.tan,
    "cot": sympy.cot,
    "sec": sympy.sec,
    "csc": sympy.csc,
    "ln": sympy.log,
}


def number(text: str) -> sympy.Rational:
    whole, _, fraction = text.partition(".")
    if not fraction:
        return sympy.Integer(int(whole))
    return sympy.Rational(int((whole or "0") + fraction), 10 ** len(fraction))


def negate(value: sympy.Expr) -> sympy.Expr:
    return sympy.Mul(sympy.Integer(-1), value, evaluate=False)


def add(terms: Sequence[sympy.Expr]) -> sympy.Expr:
    return terms[0] if len(terms) == 1 else sympy.Add(*terms, evaluate=False)


def multiply(factors: Sequence[sympy.Expr]) -> sympy.Expr:
    return factors[0] if len(factors) == 1 else sympy.Mul(*factors, evaluate=False)


def reciprocal(value: sympy.Expr) -> sympy.Expr:
    return sympy.Pow(value, sympy.Integer(-1), evaluate=False)


def divide(numerator: sympy.Expr, denominator: sympy.Expr) -> sympy.Expr:
    return sympy.Mul(numerator, reciprocal(denominator), evaluate=False)


def power(base: sympy.Expr, exponent: sympy.Expr, limits: CasLimits) -> sympy.Expr:
    check_exponent(exponent, limits)
    value = sympy.Pow(base, exponent, evaluate=False)
    if magnitude(value) > limits.max_magnitude:
        raise MathParseError("power too large")
    return value


# A root is the power radicand^(1/index), so a tiny or zero index is bounded as a large exponent.
def root(radicand: sympy.Expr, index: sympy.Expr | None, limits: CasLimits) -> sympy.Expr:
    exponent = sympy.Rational(1, 2) if index is None else reciprocal(index)
    return power(radicand, exponent, limits)


def apply_function(name: str, argument: sympy.Expr) -> sympy.Expr:
    return FUNCTIONS[name](argument, evaluate=False)


def logarithm(argument: sympy.Expr, base: sympy.Expr) -> sympy.Expr:
    return divide(sympy.log(argument, evaluate=False), sympy.log(base, evaluate=False))


def check_exponent(exponent: sympy.Expr, limits: CasLimits) -> None:
    if not exponent.is_number:
        return
    value = abs(sympy.N(exponent, EXPONENT_DIGITS))
    if not value.is_finite or value > limits.max_exponent:
        raise MathParseError("exponent too large")


# An upper bound on the decimal digits of a value, with each symbol counted as one digit.
# Powers multiply it, so a tower of individually small exponents is rejected unevaluated.
def magnitude(value: sympy.Basic) -> float:
    if isinstance(value, sympy.Rational):
        return float(len(str(abs(value.p))) + len(str(value.q)))
    if isinstance(value, sympy.Pow):
        base, exponent = value.args
        scale = abs(sympy.N(exponent, EXPONENT_DIGITS)) if exponent.is_number else None
        factor = magnitude(exponent) if scale is None else float(scale)
        return magnitude(base) * max(1.0, factor)
    return max(1.0, sum(magnitude(argument) for argument in value.args))


def is_unreduced_fraction(numerator: sympy.Expr, denominator: sympy.Expr) -> bool:
    if not isinstance(numerator, sympy.Integer) or not isinstance(denominator, sympy.Integer):
        return False
    top, bottom = abs(int(numerator)), abs(int(denominator))
    return bottom != 0 and (gcd(top, bottom) > 1 or bottom == 1)
