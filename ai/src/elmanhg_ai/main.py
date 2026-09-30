from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from typing import Final

import structlog
from fastapi import FastAPI
from fastapi.routing import APIRoute

from elmanhg_ai.api import health
from elmanhg_ai.api.chat import router as chat_router
from elmanhg_ai.api.embeddings import router as embeddings_router
from elmanhg_ai.api.essay_grades import router as essay_grades_router
from elmanhg_ai.api.transcriptions import router as transcriptions_router
from elmanhg_ai.clients.embedding import EmbeddingClient, build_embedding_client
from elmanhg_ai.clients.metered import (
    AiMetrics,
    MeteredEmbeddingClient,
    MeteredModelClient,
    MeteredTranscriptionClient,
)
from elmanhg_ai.clients.model import ModelClient, build_model_client
from elmanhg_ai.clients.transcription import TranscriptionClient, build_transcription_client
from elmanhg_ai.core.logging import configure_logging
from elmanhg_ai.core.middleware import RequestContextMiddleware
from elmanhg_ai.core.problems import register_problem_handlers
from elmanhg_ai.core.telemetry import Telemetry, build_telemetry, instrument_app
from elmanhg_ai.pipelines.chat import load_chat_prompts
from elmanhg_ai.pipelines.essay_grading import load_essay_grading_prompts
from elmanhg_ai.settings import Settings, get_settings

logger: Final = structlog.stdlib.get_logger(__name__)
INSTRUMENTATION_SCOPE: Final = "elmanhg_ai"


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    settings: Settings = app.state.settings
    telemetry: Telemetry = app.state.telemetry
    app.state.chat_prompts = load_chat_prompts(settings.chat_prompt_version)
    app.state.essay_grading_prompts = load_essay_grading_prompts(
        settings.essay_grading_prompt_version
    )
    model_client: ModelClient = app.state.injected_model_client or build_model_client(settings)
    embedding_client: EmbeddingClient = (
        app.state.injected_embedding_client or build_embedding_client(settings)
    )
    transcription_client: TranscriptionClient = (
        app.state.injected_transcription_client or build_transcription_client(settings)
    )
    metrics = AiMetrics(telemetry.meter_provider.get_meter(INSTRUMENTATION_SCOPE))
    tracer = telemetry.tracer_provider.get_tracer(INSTRUMENTATION_SCOPE)
    app.state.model_client = MeteredModelClient(
        model_client, metrics=metrics, tracer=tracer, settings=settings
    )
    app.state.embedding_client = MeteredEmbeddingClient(
        embedding_client, metrics=metrics, tracer=tracer, settings=settings
    )
    app.state.transcription_client = MeteredTranscriptionClient(
        transcription_client, metrics=metrics, tracer=tracer, settings=settings
    )
    logger.info(
        "service.started",
        env=settings.env,
        llm_provider=settings.llm_provider,
        chat_model=settings.chat_model,
        prompt_version=settings.chat_prompt_version,
        essay_grading_model=settings.essay_grading_model,
        essay_grading_prompt_version=settings.essay_grading_prompt_version,
        embedding_provider=settings.embedding_provider,
        embedding_model=settings.embedding_model,
        transcription_provider=settings.transcription_provider,
        transcription_model=settings.transcription_model,
        otlp_exporting=telemetry.exporting,
    )
    try:
        yield
    finally:
        await model_client.aclose()
        await embedding_client.aclose()
        await transcription_client.aclose()
        telemetry.shutdown()


def operation_id(route: APIRoute) -> str:
    return f"{route.tags[0]}_{route.name}"


def create_app(
    settings: Settings | None = None,
    *,
    model_client: ModelClient | None = None,
    embedding_client: EmbeddingClient | None = None,
    transcription_client: TranscriptionClient | None = None,
    telemetry: Telemetry | None = None,
) -> FastAPI:
    settings = settings or get_settings()
    telemetry = telemetry or build_telemetry(settings)
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
    app.state.injected_transcription_client = transcription_client
    app.state.telemetry = telemetry
    register_problem_handlers(app)
    app.add_middleware(RequestContextMiddleware)
    instrument_app(app, telemetry)
    app.include_router(health.router)
    app.include_router(chat_router.router)
    app.include_router(embeddings_router.router)
    app.include_router(transcriptions_router.router)
    app.include_router(essay_grades_router.router)
    return app
