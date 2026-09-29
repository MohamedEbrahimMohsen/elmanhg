import logging
import re
import secrets
import time
import uuid
from typing import Final

import structlog
from opentelemetry import trace
from starlette.middleware.base import BaseHTTPMiddleware, RequestResponseEndpoint
from starlette.requests import Request
from starlette.responses import Response

TRACEPARENT_PATTERN: Final = re.compile(r"^[0-9a-f]{2}-([0-9a-f]{32})-[0-9a-f]{16}-[0-9a-f]{2}$")
REQUEST_ID_PATTERN: Final = re.compile(r"^[A-Za-z0-9._-]{1,128}$")
ZERO_TRACE_ID: Final = "0" * 32

logger: Final = structlog.stdlib.get_logger(__name__)


def trace_id_from(traceparent: str | None) -> str | None:
    match = TRACEPARENT_PATTERN.match(traceparent or "")
    if match is None or match.group(1) == ZERO_TRACE_ID:
        return None
    return match.group(1)


def current_span_trace_id() -> str | None:
    context = trace.get_current_span().get_span_context()
    return trace.format_trace_id(context.trace_id) if context.is_valid else None


class RequestContextMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next: RequestResponseEndpoint) -> Response:
        structlog.contextvars.clear_contextvars()
        trace_id = (
            current_span_trace_id()
            or trace_id_from(request.headers.get("traceparent"))
            or secrets.token_hex(16)
        )
        request_id_header = request.headers.get("X-Request-Id", "")
        request_id = (
            request_id_header if REQUEST_ID_PATTERN.match(request_id_header) else uuid.uuid4().hex
        )
        structlog.contextvars.bind_contextvars(trace_id=trace_id, request_id=request_id)
        started = time.perf_counter()
        response = await call_next(request)
        response.headers["X-Trace-Id"] = trace_id
        response.headers["X-Request-Id"] = request_id
        path = request.url.path
        logger.log(
            logging.DEBUG if path.startswith("/health") else logging.INFO,
            "request.completed",
            method=request.method,
            path=path,
            status_code=response.status_code,
            duration_ms=round((time.perf_counter() - started) * 1000, 1),
        )
        return response
