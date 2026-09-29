import logging
import sys
from typing import Final

import structlog
from structlog.stdlib import ProcessorFormatter
from structlog.typing import Processor

from elmanhg_ai.settings import Settings

UVICORN_LOGGERS: Final = ("uvicorn", "uvicorn.error")
UVICORN_ACCESS_LOGGER: Final = "uvicorn.access"


def configure_logging(settings: Settings) -> None:
    shared: list[Processor] = [
        structlog.contextvars.merge_contextvars,
        structlog.stdlib.add_log_level,
        structlog.processors.TimeStamper(fmt="iso", utc=True),
    ]
    rendering: list[Processor] = (
        [structlog.processors.format_exc_info, structlog.processors.JSONRenderer()]
        if settings.log_format == "json"
        else [structlog.dev.ConsoleRenderer()]
    )
    handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(
        ProcessorFormatter(
            foreign_pre_chain=shared,
            processors=[ProcessorFormatter.remove_processors_meta, *rendering],
        )
    )
    root = logging.getLogger()
    root.handlers = [handler]
    root.setLevel(settings.log_level)
    for name in UVICORN_LOGGERS:
        uvicorn_logger = logging.getLogger(name)
        uvicorn_logger.handlers = []
        uvicorn_logger.propagate = True
    access_logger = logging.getLogger(UVICORN_ACCESS_LOGGER)
    access_logger.handlers = []
    access_logger.propagate = False
    structlog.configure(
        processors=[*shared, ProcessorFormatter.wrap_for_formatter],
        logger_factory=structlog.stdlib.LoggerFactory(),
        wrapper_class=structlog.stdlib.BoundLogger,
        cache_logger_on_first_use=False,
    )


def current_trace_id() -> str | None:
    trace_id = structlog.contextvars.get_contextvars().get("trace_id")
    return trace_id if isinstance(trace_id, str) else None
