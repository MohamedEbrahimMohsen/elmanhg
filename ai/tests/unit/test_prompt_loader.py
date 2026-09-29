import pytest

from elmanhg_ai.prompts.loader import PromptNotFoundError, load_prompt, render


def test_load_prompt_known_version_returns_text() -> None:
    prompt = load_prompt("avatar_system", "v1")

    assert "<lesson_context>" in prompt.text
    assert prompt.version == "v1"


def test_load_prompt_unknown_version_raises_prompt_not_found() -> None:
    with pytest.raises(PromptNotFoundError):
        load_prompt("avatar_system", "v999")


def test_render_substituted_values_are_not_rescanned() -> None:
    rendered = render("{{a}}-{{b}}", {"a": "{{b}}", "b": "x"})

    assert rendered == "{{b}}-x"
