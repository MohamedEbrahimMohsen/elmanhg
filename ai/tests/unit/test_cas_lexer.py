from typing import Final

import pytest

from elmanhg_ai.cas.lexer import Token, tokenize
from elmanhg_ai.cas.models import CasLimits, MathParseError

LIMITS: Final = CasLimits(
    max_tokens=300,
    max_depth=30,
    max_elements=10,
    max_number_digits=30,
    max_exponent=1000,
    max_magnitude=10000,
    max_expansion_terms=500,
)
ARABIC_TWO: Final = chr(0x0662)
ARABIC_FIVE: Final = chr(0x0665)
ARABIC_DECIMAL: Final = chr(0x066B)


def test_tokenize_arabic_digits_returns_latin_numbers() -> None:
    tokens = tokenize(f"{ARABIC_TWO}{ARABIC_DECIMAL}{ARABIC_FIVE}", LIMITS)

    assert tokens == [Token("number", "2.5")]


def test_tokenize_unicode_operators_returns_commands() -> None:
    tokens = tokenize("×÷≤≥≠±π√−", LIMITS)

    assert tokens == [
        Token("command", "times"),
        Token("command", "div"),
        Token("command", "le"),
        Token("command", "ge"),
        Token("command", "ne"),
        Token("command", "pm"),
        Token("command", "pi"),
        Token("command", "sqrt"),
        Token("symbol", "-"),
    ]


def test_tokenize_text_group_is_removed() -> None:
    tokens = tokenize("2\\text{ سم}", LIMITS)

    assert tokens == [Token("number", "2")]


def test_tokenize_left_right_and_spacing_are_skipped() -> None:
    tokens = tokenize("\\left( x \\, \\right)", LIMITS)

    assert tokens == [Token("symbol", "("), Token("name", "x"), Token("symbol", ")")]


def test_tokenize_subscript_name_returns_single_name() -> None:
    tokens = tokenize("x_1+x_{12}", LIMITS)

    assert tokens == [Token("name", "x_1"), Token("symbol", "+"), Token("name", "x_12")]


def test_tokenize_unknown_command_raises() -> None:
    with pytest.raises(MathParseError):
        tokenize("\\input{a}", LIMITS)


@pytest.mark.parametrize(
    "text", ["__import__", "x;", f"{ARABIC_TWO} سم"], ids=["dunder", "semicolon", "arabic-word"]
)
def test_tokenize_unexpected_character_raises(text: str) -> None:
    with pytest.raises(MathParseError):
        tokenize(text, LIMITS)


def test_tokenize_too_many_tokens_raises() -> None:
    with pytest.raises(MathParseError):
        tokenize("x" * 301, LIMITS)


def test_tokenize_long_number_raises() -> None:
    with pytest.raises(MathParseError):
        tokenize("1" * 31, LIMITS)
