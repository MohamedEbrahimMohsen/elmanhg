import re
from collections.abc import Collection, Sequence
from typing import Final

ARABIC_LETTER: Final = re.compile(r"[\u0600-\u06FF\u0750-\u077F]")
TASHKEEL: Final = re.compile(r"[\u064B-\u0652]")
STEP_LINE: Final = re.compile(
    r"^\s*(?:[0-9\u0660-\u0669]+\s*[.)\-\u2013:]|[-\u2022*])\s+", re.MULTILINE
)


def _normalise(text: str) -> str:
    return TASHKEEL.sub("", text).casefold()


def score_language(reply: str, min_ratio: float) -> bool:
    letters = [character for character in reply if character.isalpha()]
    if not letters:
        return False
    arabic = sum(1 for character in letters if ARABIC_LETTER.match(character))
    return arabic / len(letters) >= min_ratio


def score_length(reply: str, max_words: int) -> bool:
    return len(reply.split()) <= max_words


def score_citations(citations: Sequence[str], known: Collection[str], *, required: bool) -> bool:
    if required and not citations:
        return False
    return all(citation in known for citation in citations)


def score_numbered_steps(reply: str) -> bool:
    return len(STEP_LINE.findall(reply)) >= 2


def score_includes_any(reply: str, groups: Sequence[Sequence[str]]) -> bool:
    normalised = _normalise(reply)
    return all(any(_normalise(term) in normalised for term in group) for group in groups)


def score_excludes(reply: str, terms: Sequence[str]) -> bool:
    normalised = _normalise(reply)
    return not any(_normalise(term) in normalised for term in terms)


def score_total_error(awarded: int, reference: int, max_points: int) -> float:
    if max_points <= 0:
        raise ValueError("max_points must be positive")
    return abs(awarded - reference) / max_points


def score_within(awarded: int, reference: int, tolerance: int) -> bool:
    return abs(awarded - reference) <= tolerance
