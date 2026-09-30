from collections.abc import Iterator, Sequence

import sympy
from sympy.polys.polyerrors import PolificationFailed, PolynomialError

from elmanhg_ai.cas.elements import ParsedElement, raw_value_view, value_view
from elmanhg_ai.cas.models import AnswerForm


def satisfies_form(pairs: Sequence[tuple[ParsedElement, ParsedElement]], form: AnswerForm) -> bool:
    return all(_pair_ok(answer, expected, form) for answer, expected in pairs)


def _pair_ok(answer: ParsedElement, expected: ParsedElement, form: AnswerForm) -> bool:
    match form:
        case AnswerForm.EQUIVALENT:
            return True
        case AnswerForm.EXACT:
            return not answer.has_decimal
        case AnswerForm.EXPANDED:
            return all(
                sympy.expand(value) == value and not _has_product_of_sum(raw)
                for value, raw in zip(value_view(answer), raw_value_view(answer), strict=True)
            )
        case AnswerForm.FACTORED:
            return all(_is_factored(raw) for raw in raw_value_view(answer))
        case AnswerForm.SIMPLIFIED:
            answer_ops = sum(sympy.count_ops(raw) for raw in raw_value_view(answer))
            expected_ops = sum(sympy.count_ops(raw) for raw in raw_value_view(expected))
            return not answer.has_unreduced_fraction and answer_ops <= expected_ops


def _has_product_of_sum(raw: sympy.Expr) -> bool:
    return any(
        node.is_Mul and any(arg.is_Add and arg.free_symbols for arg in node.args)
        for node in sympy.preorder_traversal(raw)
    )


def _is_factored(raw: sympy.Expr) -> bool:
    return all(_is_irreducible(factor) for factor in _factors(raw))


def _factors(raw: sympy.Expr) -> Iterator[sympy.Expr]:
    if raw.is_Mul:
        for arg in raw.args:
            yield from _factors(arg)
        return
    yield raw


def _is_irreducible(factor: sympy.Expr) -> bool:
    base = factor.base if factor.is_Pow and factor.exp.is_Integer else factor
    if not base.free_symbols:
        return True
    try:
        coefficient, parts = sympy.factor_list(sympy.expand(base.doit()))
    except (PolificationFailed, PolynomialError):
        return True
    return sum(multiplicity for _, multiplicity in parts) == 1 and bool(abs(coefficient) == 1)
