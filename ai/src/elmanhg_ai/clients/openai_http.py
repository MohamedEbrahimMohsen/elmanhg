from collections.abc import Awaitable, Callable
from typing import Final

import httpx2
import structlog

from elmanhg_ai.core.errors import ModelUnavailableError

PROVIDER: Final = "openai"
OPENAI_BASE_URL: Final = "https://api.openai.com/v1"
RETRY_BASE_SECONDS: Final = 0.5
RETRY_STATUSES: Final = frozenset({429, 500, 502, 503, 504})

logger: Final = structlog.stdlib.get_logger(__name__)


async def post_with_retries(
    send: Callable[[], Awaitable[httpx2.Response]],
    *,
    max_retries: int,
    sleep: Callable[[float], Awaitable[None]],
    failure_event: str,
    model: str,
) -> httpx2.Response:
    status_code: int | None = None
    error_type = "HTTPStatusError"
    for attempt in range(max_retries + 1):
        try:
            response = await send()
        except httpx2.TransportError as error:
            status_code, error_type = None, type(error).__name__
        else:
            if response.is_success:
                return response
            status_code, error_type = response.status_code, "HTTPStatusError"
            if status_code not in RETRY_STATUSES:
                break
        if attempt < max_retries:
            await sleep(RETRY_BASE_SECONDS * 2**attempt)
    logger.warning(
        failure_event,
        provider=PROVIDER,
        model=model,
        status_code=status_code,
        error_type=error_type,
    )
    raise ModelUnavailableError()
