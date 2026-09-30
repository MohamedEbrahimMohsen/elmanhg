from dataclasses import dataclass
from typing import Self

import sympy

from elmanhg_ai.cas.models import RelationOperator


@dataclass(frozen=True, slots=True)
class ParsedElement:
    operator: RelationOperator | None
    raw_left: sympy.Expr
    raw_right: sympy.Expr | None
    left: sympy.Expr
    right: sympy.Expr | None
    has_decimal: bool
    has_unreduced_fraction: bool

    @classmethod
    def build(
        cls,
        operator: RelationOperator | None,
        raw_left: sympy.Expr,
        raw_right: sympy.Expr | None,
        has_decimal: bool,
        has_unreduced_fraction: bool,
    ) -> Self:
        return cls(
            operator=operator,
            raw_left=raw_left,
            raw_right=raw_right,
            left=raw_left.doit(),
            right=None if raw_right is None else raw_right.doit(),
            has_decimal=has_decimal,
            has_unreduced_fraction=has_unreduced_fraction,
        )


def assignment(element: ParsedElement) -> tuple[sympy.Symbol, sympy.Expr, sympy.Expr] | None:
    if element.operator != RelationOperator.EQ or element.right is None:
        return None
    if element.raw_right is None:
        return None
    if element.left.is_Symbol:
        return element.left, element.right, element.raw_right
    if element.right.is_Symbol:
        return element.right, element.left, element.raw_left
    return None


def value_view(element: ParsedElement) -> tuple[sympy.Expr, ...]:
    if element.operator is None or element.right is None:
        return (element.left,)
    target = assignment(element)
    return (target[1],) if target is not None else (element.left, element.right)


def raw_value_view(element: ParsedElement) -> tuple[sympy.Expr, ...]:
    if element.operator is None or element.raw_right is None:
        return (element.raw_left,)
    target = assignment(element)
    return (target[2],) if target is not None else (element.raw_left, element.raw_right)
