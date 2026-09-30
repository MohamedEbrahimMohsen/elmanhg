import json
from pathlib import Path

import pytest

from elmanhg_ai.pipelines.chat import load_chat_prompts
from elmanhg_ai.pipelines.essay_grading import load_essay_grading_prompts
from elmanhg_ai.prompts import loader
from elmanhg_ai.prompts.loader import PromptNotFoundError, load_output_schema, load_prompt, render


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


def test_load_chat_prompts_v2_turn_has_context_and_message_placeholders() -> None:
    prompts = load_chat_prompts("v2")

    assert "{{context}}" in prompts.turn.text
    assert "{{message}}" in prompts.turn.text
    assert "{{" not in prompts.system.text
    assert prompts.version == "v2"


def test_load_output_schema_known_version_returns_json_object_text() -> None:
    schema = json.loads(load_output_schema("essay_grade_output", "v1"))

    assert schema["type"] == "object"


def test_load_output_schema_unknown_version_raises_prompt_not_found() -> None:
    with pytest.raises(PromptNotFoundError):
        load_output_schema("essay_grade_output", "v999")


def test_load_output_schema_non_object_raises_type_error(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    (tmp_path / "list_output.v1.json").write_text("[]", encoding="utf-8")
    monkeypatch.setattr(loader.importlib.resources, "files", lambda _package: tmp_path)

    with pytest.raises(TypeError):
        load_output_schema("list_output", "v1")


def test_load_essay_grading_prompts_v1_turn_has_context_and_essay_placeholders() -> None:
    prompts = load_essay_grading_prompts("v1")

    assert "{{context}}" in prompts.turn.text
    assert "{{essay}}" in prompts.turn.text
    assert "{{" not in prompts.system.text
    assert prompts.version == "v1"


def test_essay_grade_output_schema_v1_closes_every_object() -> None:
    schema = json.loads(load_output_schema("essay_grade_output", "v1"))
    criterion = schema["properties"]["criteria"]["items"]

    assert schema["additionalProperties"] is False
    assert criterion["additionalProperties"] is False
    assert set(schema["required"]) == {"criteria", "justification", "confidence"}
    assert set(criterion["required"]) == {"criterionId", "justification", "points"}
