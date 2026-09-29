from collections.abc import Callable
from typing import Any

import pytest
from pydantic import ValidationError

from elmanhg_ai.api.chat.schemas import ChatEntryPoint, ChatIn, ContextBundleIn

PayloadBuilder = Callable[..., dict[str, Any]]


def test_chat_in_valid_camel_case_payload_parses(chat_payload: PayloadBuilder) -> None:
    chat = ChatIn.model_validate(chat_payload())

    assert chat.context.entry_point is ChatEntryPoint.QUIZ_QUESTION
    assert chat.context.question is not None
    assert chat.context.question.student_answer == "٢ أوم"


def test_chat_in_unknown_field_rejected(chat_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        ChatIn.model_validate(chat_payload(foo="bar"))

    assert error.value.errors()[0]["loc"] == ("foo",)
    assert error.value.errors()[0]["type"] == "extra_forbidden"


def test_chat_in_empty_message_rejected(chat_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        ChatIn.model_validate(chat_payload(message=""))

    assert error.value.errors()[0]["loc"] == ("message",)


def test_chat_in_history_not_alternating_rejected(chat_payload: PayloadBuilder) -> None:
    history = [{"role": "assistant", "content": "a"}, {"role": "user", "content": "b"}]

    with pytest.raises(ValidationError) as error:
        ChatIn.model_validate(chat_payload(history=history))

    assert "history must alternate" in str(error.value)


def test_chat_in_history_ending_with_user_rejected(chat_payload: PayloadBuilder) -> None:
    with pytest.raises(ValidationError) as error:
        ChatIn.model_validate(chat_payload(history=[{"role": "user", "content": "a"}]))

    assert "history must alternate" in str(error.value)


def test_context_bundle_lesson_entry_without_lesson_rejected(chat_payload: PayloadBuilder) -> None:
    context = {"entryPoint": "lesson"}

    with pytest.raises(ValidationError) as error:
        ChatIn.model_validate(chat_payload(context=context))

    assert error.value.errors()[0]["loc"] == ("context",)
    assert "lesson is required" in error.value.errors()[0]["msg"]


def test_context_bundle_quiz_question_without_question_rejected(
    chat_payload: PayloadBuilder,
) -> None:
    context = dict(chat_payload()["context"])
    del context["question"]

    with pytest.raises(ValidationError) as error:
        ContextBundleIn.model_validate(context)

    assert "lesson and question are required" in str(error.value)


def test_context_bundle_global_without_lesson_accepted() -> None:
    bundle = ContextBundleIn.model_validate({"entryPoint": "global", "subjects": ["الفيزياء"]})

    assert bundle.lesson is None
