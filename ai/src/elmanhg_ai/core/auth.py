import secrets
from typing import Annotated, Final

from fastapi import Request, Security
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from elmanhg_ai.core.errors import UnauthenticatedError
from elmanhg_ai.settings import Settings

_bearer: Final = HTTPBearer(auto_error=False)


async def require_service_token(
    request: Request,
    credentials: Annotated[HTTPAuthorizationCredentials | None, Security(_bearer)],
) -> None:
    settings: Settings = request.app.state.settings
    if credentials is None or not secrets.compare_digest(
        credentials.credentials.encode(), settings.service_token.get_secret_value().encode()
    ):
        raise UnauthenticatedError()
