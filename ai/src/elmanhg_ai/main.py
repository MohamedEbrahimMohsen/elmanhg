from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Final

import structlog
from fastapi import FastAPI
from fastapi.routing import APIRoute

from elmanhg_ai.api import health
from elmanhg_ai.api.chat import router as chat_router
from elmanhg_ai.api.embeddings import router as embeddings_router
from elmanhg_ai.clients.embedding import EmbeddingClient, build_embedding_client
from elmanhg_ai.clients.model import ModelClient, build_model_client
from elmanhg_ai.core.logging import configure_logging
from elmanhg_ai.core.middleware import RequestContextMiddleware
from elmanhg_ai.core.problems import register_problem_handlers
from elmanhg_ai.pipelines.chat import load_chat_prompts
from elmanhg_ai.settings import Settings, get_settings

logger: Final = structlog.stdlib.get_logger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    settings: Settings = app.state.settings
    app.state.chat_prompts = load_chat_prompts(settings.chat_prompt_version)
    model_client: ModelClient = app.state.injected_model_client or build_model_client(settings)
    app.state.model_client = model_client
    embedding_client: EmbeddingClient = (
        app.state.injected_embedding_client or build_embedding_client(settings)
    )
    app.state.embedding_client = embedding_client
    logger.info(
        "service.started",
        env=settings.env,
        llm_provider=settings.llm_provider,
        chat_model=settings.chat_model,
        prompt_version=settings.chat_prompt_version,
        embedding_provider=settings.embedding_provider,
        embedding_model=settings.embedding_model,
    )
    try:
        yield
    finally:
        await model_client.aclose()
        await embedding_client.aclose()


def operation_id(route: APIRoute) -> str:
    return f"{route.tags[0]}_{route.name}"


def create_app(
    settings: Settings | None = None,
    *,
    model_client: ModelClient | None = None,
    embedding_client: EmbeddingClient | None = None,
) -> FastAPI:
    settings = settings or get_settings()
    configure_logging(settings)
    docs = settings.env != "production"
    app = FastAPI(
        title="Elmanhg AI service",
        version="1.0.0",
        lifespan=lifespan,
        docs_url="/docs" if docs else None,
        redoc_url=None,
        openapi_url="/openapi.json" if docs else None,
        generate_unique_id_function=operation_id,
    )
    app.state.settings = settings
    app.state.injected_model_client = model_client
    app.state.injected_embedding_client = embedding_client
    register_problem_handlers(app)
    app.add_middleware(RequestContextMiddleware)
    app.include_router(health.router)
    app.include_router(chat_router.router)
    app.include_router(embeddings_router.router)
    return app
