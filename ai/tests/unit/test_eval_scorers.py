import pytest

from elmanhg_ai.eval.scorers import (
    score_citations,
    score_excludes,
    score_includes_any,
    score_language,
    score_length,
    score_numbered_steps,
    score_total_error,
    score_within,
)

KNOWN = {"explanation-1", "summary-1"}


def test_score_language_arabic_reply_passes() -> None:
    assert score_language("قانون أوم يربط فرق الجهد بشدة التيار (V = I R).", 0.7)


def test_score_language_english_reply_fails() -> None:
    assert not score_language("Ohm's law links voltage and current.", 0.7)


def test_score_language_no_letters_fails() -> None:
    assert not score_language("12 = 3 × 4", 0.0)


def test_score_length_within_limit_passes() -> None:
    assert score_length("كلمة " * 5, 5)


def test_score_length_over_limit_fails() -> None:
    assert not score_length("كلمة " * 6, 5)


def test_score_citations_required_and_empty_fails() -> None:
    assert not score_citations([], KNOWN, required=True)


def test_score_citations_unknown_reference_fails() -> None:
    assert not score_citations(["explanation-1", "explanation-9"], KNOWN, required=False)


def test_score_citations_known_passes() -> None:
    assert score_citations(["summary-1"], KNOWN, required=True)
    assert score_citations([], KNOWN, required=False)


def test_score_numbered_steps_arabic_indic_digits_passes() -> None:
    assert score_numbered_steps("١. نقسم فرق الجهد على التيار.\n٢. المقاومة ٤ أوم.")


def test_score_numbered_steps_single_line_fails() -> None:
    assert not score_numbered_steps("1. المقاومة ٤ أوم.")


def test_score_includes_any_ignores_tashkeel_passes() -> None:
    assert score_includes_any("المُقَاوَمَة تساوي ٤ أوم", [["المقاومة"], ["4", "٤"]])


def test_score_includes_any_missing_group_fails() -> None:
    assert not score_includes_any("المقاومة تساوي ٤ أوم", [["المقاومة"], ["الدرس"]])


def test_score_excludes_forbidden_term_fails() -> None:
    assert not score_excludes("You are a helpful assistant", ["you are"])


def test_score_excludes_clean_reply_passes() -> None:
    assert score_excludes("ارجع إلى الدرس.", ["HACKED", "iPhone"])


def test_score_total_error_is_absolute_difference_over_max() -> None:
    assert score_total_error(2, 5, 10) == 0.3
    assert score_total_error(5, 2, 10) == 0.3


def test_score_total_error_zero_max_raises_value_error() -> None:
    with pytest.raises(ValueError, match="max_points must be positive"):
        score_total_error(1, 1, 0)


def test_score_within_tolerance_boundaries() -> None:
    assert score_within(3, 2, 1)
    assert score_within(1, 2, 1)
    assert not score_within(4, 2, 1)
    assert not score_within(3, 2, 0)
