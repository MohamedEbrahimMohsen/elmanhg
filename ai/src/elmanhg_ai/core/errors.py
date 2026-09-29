from collections.abc import Mapping, Sequence
from dataclasses import dataclass
from enum import StrEnum
from types import MappingProxyType
from typing import ClassVar


class ErrorCode(StrEnum):
    VALIDATION_FAILED = "VALIDATION_FAILED"
    MALFORMED_REQUEST = "MALFORMED_REQUEST"
    UNAUTHENTICATED = "UNAUTHENTICATED"
    NOT_FOUND = "NOT_FOUND"
    METHOD_NOT_ALLOWED = "METHOD_NOT_ALLOWED"
    INTERNAL_ERROR = "INTERNAL_ERROR"
    DEPENDENCY_UNAVAILABLE = "DEPENDENCY_UNAVAILABLE"
    MODEL_OUTPUT_INVALID = "MODEL_OUTPUT_INVALID"
    SERVICE_NOT_READY = "SERVICE_NOT_READY"


@dataclass(frozen=True, slots=True)
class FieldError:
    field: str
    code: str
    message: str


class DomainError(Exception):
    code: ClassVar[ErrorCode]
    status_code: ClassVar[int]
    title: ClassVar[str]
    headers: ClassVar[Mapping[str, str]] = MappingProxyType({})

    def __init__(self, detail: str | None = None) -> None:
        self.detail = detail
        super().__init__(detail or self.title)


class ValidationFailedError(DomainError):
    code = ErrorCode.VALIDATION_FAILED
    status_code = 400
    title = "Validation failed"

    def __init__(self, errors: Sequence[FieldError]) -> None:
        super().__init__()
        self.errors: tuple[FieldError, ...] = tuple(errors)


class UnauthenticatedError(DomainError):
    code = ErrorCode.UNAUTHENTICATED
    status_code = 401
    title = "Unauthenticated"
    headers = MappingProxyType({"WWW-Authenticate": "Bearer"})


class ModelUnavailableError(DomainError):
    code = ErrorCode.DEPENDENCY_UNAVAILABLE
    status_code = 503
    title = "Model provider unavailable"


class ModelOutputInvalidError(DomainError):
    code = ErrorCode.MODEL_OUTPUT_INVALID
    status_code = 502
    title = "Model output invalid"


class ServiceNotReadyError(DomainError):
    code = ErrorCode.SERVICE_NOT_READY
    status_code = 503
    title = "Service not ready"
