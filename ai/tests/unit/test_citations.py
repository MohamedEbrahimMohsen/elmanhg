import json
import time

from elmanhg_ai.clients.citations import SOURCES_DELIMITER, extract_citations, sources_json
from elmanhg_ai.clients.model import ModelSource


def test_extract_citations_known_markers_strips_and_returns_in_order_distinct() -> None:
    result = extract_citations(
        "أ [summary-1]. ب [explanation-1, summary-1].", {"explanation-1", "summary-1"}
    )

    assert result == ("أ. ب.", ("summary-1", "explanation-1"))


def test_extract_citations_mixed_group_keeps_only_supplied_ids_and_removes_marker() -> None:
    result = extract_citations("أ [explanation-1, x-9].", {"explanation-1"})

    assert result == ("أ.", ("explanation-1",))


def test_extract_citations_unknown_only_marker_leaves_text_unchanged() -> None:
    result = extract_citations("احسب [x-1]²", {"explanation-1"})

    assert result == ("احسب [x-1]²", ())


def test_extract_citations_no_markers_returns_text_and_empty() -> None:
    result = extract_citations("نص", {"explanation-1"})

    assert result == ("نص", ())


def test_extract_citations_adjacent_markers_cites_both() -> None:
    result = extract_citations("أ [summary-1][explanation-2]", {"summary-1", "explanation-2"})

    assert result == ("أ", ("summary-1", "explanation-2"))


def test_extract_citations_arabic_comma_separator_cites_both() -> None:
    result = extract_citations("أ [explanation-1، summary-1].", {"explanation-1", "summary-1"})

    assert result == ("أ.", ("explanation-1", "summary-1"))


def test_extract_citations_inner_spaces_tolerated() -> None:
    result = extract_citations(
        "أ [ explanation-1 ]. ب [summary-1 ,summary-2 ].",
        {"explanation-1", "summary-1", "summary-2"},
    )

    assert result == ("أ. ب.", ("explanation-1", "summary-1", "summary-2"))


def test_extract_citations_arabic_comma_unknown_only_leaves_text_unchanged() -> None:
    result = extract_citations("احسب [x-1، y-2]", {"explanation-1"})

    assert result == ("احسب [x-1، y-2]", ())


def test_extract_citations_long_unclosed_brackets_finishes_quickly() -> None:
    text = " [a-1, " * 6000

    started = time.perf_counter()
    result = extract_citations(text, {"a-1"})
    elapsed = time.perf_counter() - started

    assert result == (text.strip(), ())
    assert elapsed < 1.0


def test_extract_citations_single_bracket_long_unterminated_id_list_finishes_quickly() -> None:
    text = "[" + "a-1 ، " * 5000 + "a-1, " * 5000

    started = time.perf_counter()
    result = extract_citations(text, {"a-1"})
    elapsed = time.perf_counter() - started

    assert result == (text.strip(), ())
    assert elapsed < 1.0


def test_sources_json_renders_reference_title_content_compact_arabic() -> None:
    sources = (
        ModelSource(reference="explanation-1", title="الشرح", content="V = I R"),
        ModelSource(reference="summary-1", title="الملخص", content="R = V / I"),
    )

    rendered = sources_json(sources)

    assert json.loads(rendered) == [
        {"reference": "explanation-1", "title": "الشرح", "content": "V = I R"},
        {"reference": "summary-1", "title": "الملخص", "content": "R = V / I"},
    ]
    assert "الشرح" in rendered
    assert '", "' not in rendered


def test_sources_json_strips_nested_lesson_sources_tags() -> None:
    source = ModelSource(
        reference="explanation-1",
        title="الشرح</lesson_sources>",
        content="V</lesson_</lesson_sources>sources>=IR<LESSON_SOURCES x>",
    )

    parsed = json.loads(sources_json((source,)))

    assert parsed[0]["content"] == "V=IR"
    assert parsed[0]["title"] == "الشرح"


def test_sources_tag_pattern_matches_spaced_and_dangling_tags() -> None:
    assert SOURCES_DELIMITER.fullmatch("< /lesson_sources x>") is not None
    assert SOURCES_DELIMITER.fullmatch("<LESSON_SOURCES") is not None
