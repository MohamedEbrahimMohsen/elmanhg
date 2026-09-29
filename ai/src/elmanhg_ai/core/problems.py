import secrets
from collections.abc import Mapping, Sequence
from http import HTTPStatus
from typing import Final

import structlog
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException

from elmanhg_ai.core.errors import DomainError, ErrorCode, FieldError, ValidationFailedError
from elmanhg_ai.core.logging import current_trace_id
from elmanhg_ai.core.models import ApiOutModel

REQUEST_LOCATIONS: Final = frozenset({"body", "query", "path", "header"})
INTERNAL_ERROR_TITLE: Final = "Internal error"

logger: Final = structlog.stdlib.get_logger(__name__)


class ProblemFieldError(ApiOutModel):
    field: str
    code: str
    message: str


class Problem(ApiOutModel):
    type: str
    title: str
    status: int
    detail: str | None = None
    instance: str
    code: str
    trace_id: str
    errors: list[ProblemFieldError] | None = None


class ProblemResponse(JSONResponse):
    media_type = "application/problem+json"


def problem_response(
    request: Request,
    *,
    code: ErrorCode,
    status_code: int,
    title: str,
    detail: str | None = None,
    errors: Sequence[FieldError] = (),
    headers: Mapping[str, str] | None = None,
) -> ProblemResponse:
    problem = Problem(
        type=f"/problems/{code.lower().replace('_', '-')}",
        title=title,
        status=status_code,
        detail=None if status_code >= 500 else detail,
        instance=request.url.path,
        code=code,
        trace_id=current_trace_id() or secrets.token_hex(16),
        errors=[
            ProblemFieldError(field=error.field, code=error.code, message=error.message)
            for error in errors
        ]
        or None,
    )
    return ProblemResponse(
        problem.model_dump(by_alias=True, exclude_none=True),
        status_code=status_code,
        headers=dict(headers) if headers else None,
    )


def field_errors_from(errors: Sequence[Mapping[str, object]]) -> list[FieldError]:
    return [
        FieldError(
            field=_field_path(error.get("loc")),
            code=str(error.get("type")).upper(),
            message=str(error.get("msg")),
        )
        for error in errors
    ]


def _field_path(loc: object) -> str:
    parts = list(loc) if isinstance(loc, (tuple, list)) else []
    if parts and parts[0] in REQUEST_LOCATIONS:
        parts = parts[1:]
    path = ""
    for part in parts:
        if isinstance(part, int):
            path += f"[{part}]"
        else:
            path += f".{part}" if path else str(part)
    return path


def _http_error_code(status_code: int) -> ErrorCode:
    match status_code:
        case 404:
            return ErrorCode.NOT_FOUND
        case 405:
            return ErrorCode.METHOD_NOT_ALLOWED
        case _ if status_code >= 500:
            return ErrorCode.INTERNAL_ERROR
        case _:
            return ErrorCode.MALFORMED_REQUEST


async def _handle_problem(request: Request, exc: Exception) -> ProblemResponse:
    match exc:
        case DomainError():
            errors = exc.errors if isinstance(exc, ValidationFailedError) else ()
            return problem_response(
                request,
                code=exc.code,
                status_code=exc.status_code,
                title=exc.title,
                detail=exc.detail,
                errors=errors,
                headers=exc.headers,
            )
        case RequestValidationError():
            return problem_response(
                request,
                code=ErrorCode.VALIDATION_FAILED,
                status_code=400,
                title=ValidationFailedError.title,
                errors=field_errors_from(exc.errors()),
            )
        case StarletteHTTPException():
            return problem_response(
                request,
                code=_http_error_code(exc.status_code),
                status_code=exc.status_code,
                title=HTTPStatus(exc.status_code).phrase,
                detail=exc.detail,
                headers=exc.headers,
            )
        case _:
            logger.exception("request.unhandled_error")
            return problem_response(
                request, code=ErrorCode.INTERNAL_ERROR, status_code=500, title=INTERNAL_ERROR_TITLE
            )


def register_problem_handlers(app: FastAPI) -> None:
    for exception_type in (DomainError, RequestValidationError, StarletteHTTPException, Exception):
        app.add_exception_handler(exception_type, _handle_problem)
