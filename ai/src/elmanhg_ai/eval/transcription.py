import re
import unicodedata
from typing import Final

WER_THRESHOLD: Final = 0.35
TASHKEEL: Final = range(0x064B, 0x0653)
TATWEEL: Final = 0x0640
ALEF: Final = 0x0627
ALEF_VARIANTS: Final = (0x0622, 0x0623, 0x0625)
ALEF_MAKSURA: Final = 0x0649
YEH: Final = 0x064A
TEH_MARBUTA: Final = 0x0629
HEH: Final = 0x0647
ARABIC_FOLDING: Final = str.maketrans(
    {chr(code): None for code in (*TASHKEEL, TATWEEL)}
    | {chr(code): chr(ALEF) for code in ALEF_VARIANTS}
    | {chr(ALEF_MAKSURA): chr(YEH), chr(TEH_MARBUTA): chr(HEH)}
)
PUNCTUATION: Final = re.compile(r"[^\w\s]")


def normalise_arabic(text: str) -> tuple[str, ...]:
    folded = unicodedata.normalize("NFKC", text).casefold().translate(ARABIC_FOLDING)
    return tuple(PUNCTUATION.sub("", folded).split())


def score_word_error_rate(reference: str, hypothesis: str) -> float:
    expected = normalise_arabic(reference)
    if not expected:
        raise ValueError("reference is empty")
    actual = normalise_arabic(hypothesis)
    previous = list(range(len(actual) + 1))
    for row, word in enumerate(expected, start=1):
        current = [row]
        for column, candidate in enumerate(actual, start=1):
            substitution = previous[column - 1] + (word != candidate)
            current.append(min(previous[column] + 1, current[column - 1] + 1, substitution))
        previous = current
    return previous[-1] / len(expected)
