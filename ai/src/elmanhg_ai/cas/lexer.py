import re
from dataclasses import dataclass
from typing import Final, Literal

from elmanhg_ai.cas.models import CasLimits, MathParseError

TokenKind = Literal["number", "name", "command", "symbol"]


@dataclass(frozen=True, slots=True)
class Token:
    kind: TokenKind
    text: str


ARABIC_INDIC_ZERO: Final = 0x0660
EXTENDED_ARABIC_INDIC_ZERO: Final = 0x06F0
ARABIC_DECIMAL_SEPARATOR: Final = 0x066B
ARABIC_COMMA: Final = 0x060C
TRANSLATION: Final = str.maketrans(
    {chr(ARABIC_INDIC_ZERO + digit): str(digit) for digit in range(10)}
    | {chr(EXTENDED_ARABIC_INDIC_ZERO + digit): str(digit) for digit in range(10)}
    | {chr(ARABIC_DECIMAL_SEPARATOR): ".", chr(ARABIC_COMMA): ","}
)
TEXT_GROUP: Final = re.compile(r"\\text\s*\{[^{}]*\}")
NUMBER: Final = re.compile(r"[0-9]+(?:\.[0-9]+)?|\.[0-9]+")
NAME: Final = re.compile(r"([A-Za-z])(?:_(?:([A-Za-z0-9])|\{([A-Za-z0-9]{1,5})\}))?")
COMMAND_NAME: Final = re.compile(r"[A-Za-z]+")
SPACING: Final = frozenset({",", ";", ":", "!", " "})
SKIPPED: Final = frozenset({"left", "right", "quad", "qquad", "displaystyle"})
ALIASES: Final = {"dfrac": "frac", "tfrac": "frac", "leq": "le", "geq": "ge", "neq": "ne"}
SYMBOL_COMMANDS: Final = {"lt": "<", "gt": ">"}
COMMANDS: Final = frozenset(
    {
        "frac", "sqrt", "pi", "times", "cdot", "div", "pm", "le", "ge", "ne",
        "sin", "cos", "tan", "cot", "sec", "csc", "log", "ln",
        "alpha", "beta", "gamma", "theta", "lambda", "mu", "phi", "omega",
    }
)  # fmt: skip
UNICODE: Final = {
    "×": "times",
    "÷": "div",
    "·": "cdot",
    "⋅": "cdot",
    "≤": "le",
    "≥": "ge",
    "≠": "ne",
    "±": "pm",
    "π": "pi",
    "√": "sqrt",
}
DASHES: Final = frozenset({"−", "–"})
SYMBOLS: Final = frozenset("+-*/^=<>()[]{},")
IGNORED: Final = frozenset({"~"})


def tokenize(text: str, limits: CasLimits) -> list[Token]:
    source = TEXT_GROUP.sub(" ", text.translate(TRANSLATION))
    tokens: list[Token] = []
    position = 0
    while position < len(source):
        char = source[position]
        if char.isspace() or char in IGNORED:
            position += 1
        elif char == "\\":
            position = _backslash(source, position + 1, tokens)
        elif char in UNICODE:
            tokens.append(Token("command", UNICODE[char]))
            position += 1
        elif char in DASHES:
            tokens.append(Token("symbol", "-"))
            position += 1
        elif number := NUMBER.match(source, position):
            tokens.append(_number(number.group(), limits))
            position = number.end()
        elif name := NAME.match(source, position):
            tokens.append(Token("name", _name_text(name)))
            position = name.end()
        elif char in SYMBOLS:
            tokens.append(Token("symbol", char))
            position += 1
        else:
            raise MathParseError("unexpected character")
        if len(tokens) > limits.max_tokens:
            raise MathParseError("too many tokens")
    return tokens


def _backslash(source: str, position: int, tokens: list[Token]) -> int:
    char = source[position] if position < len(source) else ""
    if char in SPACING:
        return position + 1
    if char in {"{", "}"}:
        tokens.append(Token("symbol", "\\" + char))
        return position + 1
    match = COMMAND_NAME.match(source, position)
    if match is None:
        raise MathParseError("unexpected backslash")
    name = ALIASES.get(match.group(), match.group())
    end = match.end()
    if name in SYMBOL_COMMANDS:
        tokens.append(Token("symbol", SYMBOL_COMMANDS[name]))
    elif name in COMMANDS:
        tokens.append(Token("command", name))
        if name == "log" and source.startswith("_", end):
            tokens.append(Token("symbol", "_"))
            end += 1
    elif name not in SKIPPED:
        raise MathParseError("unknown command")
    return end


def _number(text: str, limits: CasLimits) -> Token:
    if sum(char.isdigit() for char in text) > limits.max_number_digits:
        raise MathParseError("number too long")
    return Token("number", text)


def _name_text(match: re.Match[str]) -> str:
    letter, short, long = match.groups()
    subscript = short or long
    return f"{letter}_{subscript}" if subscript else letter
