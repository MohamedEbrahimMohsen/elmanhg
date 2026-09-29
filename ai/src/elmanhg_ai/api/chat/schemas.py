from enum import StrEnum
from typing import Self
from uuid import UUID

from pydantic import Field, model_validator

from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class ChatEntryPoint(StrEnum):
    LESSON = "lesson"
    QUIZ_QUESTION = "quizQuestion"
    EXAM_REVIEW = "examReview"
    GLOBAL = "global"


class ChatRole(StrEnum):
    USER = "user"
    ASSISTANT = "assistant"


class ContextRefIn(ApiInModel):
    id: UUID
    name: str = Field(min_length=1)


class LessonContextIn(ApiInModel):
    id: UUID
    name: str = Field(min_length=1)
    explanation: str = ""
    objectives: list[str] = Field(default_factory=list)
    summary: str = ""


class QuestionContextIn(ApiInModel):
    id: UUID
    stem: str = Field(min_length=1)
    student_answer: str | None = None
    correct_answer: str | None = None
    explanation: str | None = None


class ContextBundleIn(ApiInModel):
    entry_point: ChatEntryPoint
    subject: ContextRefIn | None = None
    unit: ContextRefIn | None = None
    lesson: LessonContextIn | None = None
    question: QuestionContextIn | None = None
    subjects: list[str] = Field(default_factory=list)

    @model_validator(mode="after")
    def _entry_point_has_its_context(self) -> Self:
        match self.entry_point:
            case ChatEntryPoint.LESSON if self.lesson is None:
                raise ValueError("lesson is required for the lesson entry point")
            case ChatEntryPoint.QUIZ_QUESTION | ChatEntryPoint.EXAM_REVIEW if (
                self.lesson is None or self.question is None
            ):
                raise ValueError(
                    f"lesson and question are required for the {self.entry_point} entry point"
                )
        return self


class ChatMessageIn(ApiInModel):
    role: ChatRole
    content: str = Field(min_length=1)


class ChatSourceIn(ApiInModel):
    reference: str = Field(min_length=1, max_length=200, pattern=r"^[a-z0-9-]+$")
    title: str = Field(min_length=1, max_length=300)
    content: str = Field(min_length=1)


class ChatIn(ApiInModel):
    context: ContextBundleIn
    history: list[ChatMessageIn] = Field(default_factory=list)
    message: str = Field(min_length=1)
    sources: list[ChatSourceIn] = Field(default_factory=list)

    @model_validator(mode="after")
    def _history_alternates(self) -> Self:
        expected = (ChatRole.USER, ChatRole.ASSISTANT)
        if len(self.history) % 2 != 0 or any(
            turn.role != expected[index % 2] for index, turn in enumerate(self.history)
        ):
            raise ValueError(
                "history must alternate user and assistant turns, "
                "starting with user and ending with assistant"
            )
        return self

    @model_validator(mode="after")
    def _source_references_unique(self) -> Self:
        references = [source.reference for source in self.sources]
        if len(set(references)) != len(references):
            raise ValueError("source references must be unique")
        return self


class ChatOut(ApiOutModel):
    reply: str
    model: str
    prompt_version: str
    input_tokens: int
    output_tokens: int
    stop_reason: str | None
    citations: list[str]
