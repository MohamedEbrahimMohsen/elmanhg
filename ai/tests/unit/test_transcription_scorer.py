import pytest

from elmanhg_ai.eval.transcription import score_word_error_rate

ALEF_HAMZA_ABOVE = chr(0x0623)
ALEF_HAMZA_BELOW = chr(0x0625)
ALEF_MADDA = chr(0x0622)
ALEF = chr(0x0627)
FATHA = chr(0x064E)
SHADDA = chr(0x0651)
TATWEEL = chr(0x0640)
ALEF_MAKSURA = chr(0x0649)
YEH = chr(0x064A)
TEH_MARBUTA = chr(0x0629)
HEH = chr(0x0647)


def test_score_word_error_rate_identical_after_normalisation_is_zero() -> None:
    reference = (
        f"{ALEF_HAMZA_ABOVE}هلا مع{ALEF_MAKSURA} {ALEF_HAMZA_BELOW}جاب{TEH_MARBUTA} {ALEF_MADDA}خر"
    )
    hypothesis = f"{ALEF}{FATHA}ه{SHADDA}لا، مع{YEH} {ALEF}جا{TATWEEL}ب{HEH} {ALEF}خر."

    assert score_word_error_rate(reference, hypothesis) == 0.0


def test_score_word_error_rate_one_substitution_in_four_words_is_quarter() -> None:
    assert score_word_error_rate("one two three four", "one two tree four") == 0.25


def test_score_word_error_rate_counts_insertions_and_deletions() -> None:
    assert score_word_error_rate("one two", "one extra two") == 0.5
    assert score_word_error_rate("one two", "one") == 0.5


def test_score_word_error_rate_empty_reference_raises_value_error() -> None:
    with pytest.raises(ValueError, match="reference is empty"):
        score_word_error_rate(" ... ", "anything")
