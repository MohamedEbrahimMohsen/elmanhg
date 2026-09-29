from typing import Annotated

from fastapi import Depends, Request

from elmanhg_ai.clients.embedding import EmbeddingClient
from elmanhg_ai.clients.model import ModelClient
from elmanhg_ai.clients.transcription import TranscriptionClient
from elmanhg_ai.core.errors import ServiceNotReadyError
from elmanhg_ai.pipelines.chat import ChatPrompts
from elmanhg_ai.settings import Settings


def settings_from_app(request: Request) -> Settings:
    settings: Settings = request.app.state.settings
    return settings


def model_client_from_app(request: Request) -> ModelClient:
    model_client: ModelClient | None = getattr(request.app.state, "model_client", None)
    if model_client is None:
        raise ServiceNotReadyError()
    return model_client


def embedding_client_from_app(request: Request) -> EmbeddingClient:
    embedding_client: EmbeddingClient | None = getattr(request.app.state, "embedding_client", None)
    if embedding_client is None:
        raise ServiceNotReadyError()
    return embedding_client


def transcription_client_from_app(request: Request) -> TranscriptionClient:
    transcription_client: TranscriptionClient | None = getattr(
        request.app.state, "transcription_client", None
    )
    if transcription_client is None:
        raise ServiceNotReadyError()
    return transcription_client


def chat_prompts_from_app(request: Request) -> ChatPrompts:
    chat_prompts: ChatPrompts | None = getattr(request.app.state, "chat_prompts", None)
    if chat_prompts is None:
        raise ServiceNotReadyError()
    return chat_prompts


SettingsDep = Annotated[Settings, Depends(settings_from_app)]
ModelClientDep = Annotated[ModelClient, Depends(model_client_from_app)]
ChatPromptsDep = Annotated[ChatPrompts, Depends(chat_prompts_from_app)]
EmbeddingClientDep = Annotated[EmbeddingClient, Depends(embedding_client_from_app)]
TranscriptionClientDep = Annotated[TranscriptionClient, Depends(transcription_client_from_app)]
