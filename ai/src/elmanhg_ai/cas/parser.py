from typing import Final

import sympy

from elmanhg_ai.cas import nodes
from elmanhg_ai.cas.elements import ParsedElement
from elmanhg_ai.cas.lexer import Token, tokenize
from elmanhg_ai.cas.models import CasLimits, MathParseError, RelationOperator

RELATIONS: Final = {
    Token("symbol", "="): RelationOperator.EQ,
    Token("symbol", "<"): RelationOperator.LT,
    Token("symbol", ">"): RelationOperator.GT,
    Token("command", "le"): RelationOperator.LE,
    Token("command", "ge"): RelationOperator.GE,
    Token("command", "ne"): RelationOperator.NE,
}
GREEK: Final = frozenset({"alpha", "beta", "gamma", "theta", "lambda", "mu", "phi", "omega"})
PRIMARY_COMMANDS: Final = frozenset({"frac", "sqrt", "pi", "log", *nodes.FUNCTIONS, *GREEK})
BRACKETS: Final = {"(": ")", "[": "]", "{": "}"}
MULTIPLY: Final = frozenset(
    {Token("symbol", "*"), Token("command", "times"), Token("command", "cdot")}
)
DIVIDE: Final = frozenset({Token("symbol", "/"), Token("command", "div")})
POWER: Final = Token("symbol", "^")
SUBSCRIPT: Final = Token("symbol", "_")
SET_OPEN: Final = Token("symbol", "\\{")
SET_CLOSE: Final = Token("symbol", "\\}")
COMMA: Final = Token("symbol", ",")
PLUS_MINUS: Final = Token("command", "pm")
PLUS: Final = Token("symbol", "+")
MINUS: Final = Token("symbol", "-")
DEFAULT_LOG_BASE: Final = 10


def parse_answer(text: str, limits: CasLimits) -> tuple[ParsedElement, ...]:
    groups = _split(_unwrap_set(tokenize(text, limits)))
    if len(groups) > limits.max_elements:
        raise MathParseError("too many elements")
    return tuple(
        _ElementParser(tokens, limits).parse() for group in groups for tokens in _expand(group)
    )


def _unwrap_set(tokens: list[Token]) -> list[Token]:
    if len(tokens) < 2 or tokens[0] != SET_OPEN or tokens[-1] != SET_CLOSE:
        return tokens
    depth = 0
    for token in tokens[:-1]:
        depth += (token == SET_OPEN) - (token == SET_CLOSE)
        if depth == 0:
            return tokens
    return tokens[1:-1]


def _split(tokens: list[Token]) -> list[list[Token]]:
    groups: list[list[Token]] = [[]]
    depth = 0
    for token in tokens:
        if token == COMMA:
            if depth != 0:
                raise MathParseError("comma inside brackets")
            groups.append([])
            continue
        if token.kind == "symbol":
            depth += (token.text in BRACKETS) - (token.text in BRACKETS.values())
        groups[-1].append(token)
    if any(not group for group in groups):
        raise MathParseError("empty element")
    return groups


def _expand(tokens: list[Token]) -> list[list[Token]]:
    count = tokens.count(PLUS_MINUS)
    if count == 0:
        return [tokens]
    if count > 1:
        raise MathParseError("more than one plus-minus")
    index = tokens.index(PLUS_MINUS)
    return [[*tokens[:index], sign, *tokens[index + 1 :]] for sign in (PLUS, MINUS)]


class _ElementParser:
    def __init__(self, tokens: list[Token], limits: CasLimits) -> None:
        self.tokens = tokens
        self.position = 0
        self.depth = 0
        self.limits = limits
        self.has_decimal = False
        self.has_unreduced_fraction = False
        self.split_at: int | None = None

    def parse(self) -> ParsedElement:
        left = self.expr()
        token = self._peek()
        operator = None if token is None else RELATIONS.get(token)
        right = None
        if operator is not None:
            self.position += 1
            right = self.expr()
        if self._peek() is not None:
            raise MathParseError("unexpected token")
        return ParsedElement.build(
            operator, left, right, self.has_decimal, self.has_unreduced_fraction
        )

    def expr(self) -> sympy.Expr:
        self._enter()
        terms = [self.term()]
        while (token := self._peek()) in (PLUS, MINUS):
            self.position += 1
            value = self.term()
            terms.append(value if token == PLUS else nodes.negate(value))
        self.depth -= 1
        return nodes.add(terms)

    def term(self) -> sympy.Expr:
        factors = [self.factor()]
        while (token := self._peek()) is not None:
            if token in MULTIPLY:
                self.position += 1
                factors.append(self.factor())
            elif token in DIVIDE:
                self.position += 1
                denominator = self.factor()
                self._flag_fraction(nodes.multiply(factors), denominator)
                factors.append(nodes.reciprocal(denominator))
            elif self._starts_implicit(token):
                factors.append(self.factor())
            else:
                break
        return nodes.multiply(factors)

    def factor(self) -> sympy.Expr:
        token = self._peek()
        if token in (PLUS, MINUS):
            self.position += 1
            self._enter()
            value = self.factor()
            self.depth -= 1
            return nodes.negate(value) if token == MINUS else value
        base = self.primary()
        if self._peek() != POWER:
            return base
        self.position += 1
        value = nodes.power(base, self.single_or_group(), self.limits)
        if self._peek() == POWER:
            raise MathParseError("ambiguous power")
        return value

    def primary(self) -> sympy.Expr:
        token = self._take()
        if token.kind == "number":
            self.has_decimal = self.has_decimal or "." in token.text
            return nodes.number(token.text)
        if token.kind == "name":
            return sympy.E if token.text == "e" else sympy.Symbol(token.text)
        if token.kind == "symbol" and token.text in BRACKETS:
            return self._bracketed(token.text)
        if token.kind == "command":
            self._enter()
            value = self._command(token.text)
            self.depth -= 1
            return value
        raise MathParseError("unexpected token")

    def single_or_group(self) -> sympy.Expr:
        token = self._take()
        if token == Token("symbol", "{"):
            return self._bracketed("{")
        if token.kind == "number" and token.text[0].isdigit():
            if len(token.text) > 1:
                self.position -= 1
                self.tokens[self.position] = Token("number", token.text[1:])
                self.split_at = self.position
            return sympy.Integer(int(token.text[0]))
        if token.kind == "name":
            return sympy.E if token.text == "e" else sympy.Symbol(token.text)
        if token.kind == "command" and (token.text == "pi" or token.text in GREEK):
            return self._command(token.text)
        raise MathParseError("unexpected group")

    def _command(self, name: str) -> sympy.Expr:
        if name == "pi":
            return sympy.pi
        if name in GREEK:
            return sympy.Symbol(name)
        if name == "frac":
            numerator = self.single_or_group()
            denominator = self.single_or_group()
            self._flag_fraction(numerator, denominator)
            return nodes.divide(numerator, denominator)
        if name == "sqrt":
            index = self._bracketed("[") if self._accept(Token("symbol", "[")) else None
            return nodes.root(self.single_or_group(), index, self.limits)
        if name == "log":
            base = self.single_or_group() if self._accept(SUBSCRIPT) else None
            base = sympy.Integer(DEFAULT_LOG_BASE) if base is None else base
            return nodes.logarithm(self._function_argument(), base)
        if name in nodes.FUNCTIONS:
            exponent = self.single_or_group() if self._accept(POWER) else None
            value = nodes.apply_function(name, self._function_argument())
            return value if exponent is None else nodes.power(value, exponent, self.limits)
        raise MathParseError("unexpected command")

    def _function_argument(self) -> sympy.Expr:
        token = self._peek()
        if token is not None and token.kind == "symbol" and token.text in {"(", "{"}:
            self.position += 1
            return self._bracketed(token.text)
        return self.primary()

    def _bracketed(self, opener: str) -> sympy.Expr:
        value = self.expr()
        if not self._accept(Token("symbol", BRACKETS[opener])):
            raise MathParseError("unclosed bracket")
        return value

    def _starts_implicit(self, token: Token) -> bool:
        if token.kind == "number":
            return self.position == self.split_at
        if token.kind == "name":
            return True
        if token.kind == "command":
            return token.text in PRIMARY_COMMANDS
        return token.kind == "symbol" and token.text in BRACKETS

    def _flag_fraction(self, numerator: sympy.Expr, denominator: sympy.Expr) -> None:
        if nodes.is_unreduced_fraction(numerator, denominator):
            self.has_unreduced_fraction = True

    def _enter(self) -> None:
        self.depth += 1
        if self.depth > self.limits.max_depth:
            raise MathParseError("too deeply nested")

    def _peek(self) -> Token | None:
        return self.tokens[self.position] if self.position < len(self.tokens) else None

    def _take(self) -> Token:
        token = self._peek()
        if token is None:
            raise MathParseError("unexpected end")
        self.position += 1
        return token

    def _accept(self, expected: Token) -> bool:
        if self._peek() != expected:
            return False
        self.position += 1
        return True
