import re
import time

from elmanhg_ai.prompts.delimiters import delimiter_pattern, strip_fields, strip_tags

ESSAY_TAGS = delimiter_pattern("grading_context", "student_essay")
PREVIOUS_CHAT_PATTERN = r"<\s*(?:/\s*)?(?:lesson_context|student_message)\b[^<>]*>?"


def test_delimiter_pattern_matches_spaced_attribute_and_dangling_tags() -> None:
    for tag in ("< /student_essay x>", "<GRADING_CONTEXT", "</student_essay"):
        assert ESSAY_TAGS.fullmatch(tag) is not None, tag


def test_delimiter_pattern_for_chat_names_equals_previous_regex() -> None:
    pattern = delimiter_pattern("lesson_context", "student_message")

    assert pattern.pattern == PREVIOUS_CHAT_PATTERN
    assert pattern.flags == re.compile(PREVIOUS_CHAT_PATTERN, re.IGNORECASE).flags


def test_strip_tags_repeats_until_stable_for_nested_fragments() -> None:
    assert strip_tags("</stu</student_essay>dent_essay>", ESSAY_TAGS) == ""


def test_strip_fields_strips_nested_strings_and_keeps_other_values() -> None:
    value = {"a": "x<student_essay>y", "b": [1, None, "<grading_context>z"], "c": {"d": True}}

    assert strip_fields(value, ESSAY_TAGS) == {"a": "xy", "b": [1, None, "z"], "c": {"d": True}}


def test_strip_tags_near_cap_nested_input_finishes_quickly() -> None:
    text = "</stu" * 800 + "</student_essay>" + "dent_essay>" * 800 + "<grading_context" * 500
    assert 19_000 < len(text) <= 21_000

    started = time.perf_counter()
    stripped = strip_tags(text, ESSAY_TAGS)
    elapsed = time.perf_counter() - started

    assert stripped == ""
    assert elapsed < 2
