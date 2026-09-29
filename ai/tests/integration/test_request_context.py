import re
from collections.abc import Callable
from typing import Any

import httpx2
import pytest
from structlog.testing import LogCapture

pytestmark = pytest.mark.integration

TRACEPARENT = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736"


async def test_request_with_traceparent_echoes_trace_id(client: httpx2.AsyncClient) -> None:
    response = await client.get("/health", headers={"traceparent": TRACEPARENT})

    assert response.headers["X-Trace-Id"] == TRACE_ID


async def test_request_without_traceparent_generates_trace_id(client: httpx2.AsyncClient) -> None:
    response = await client.get("/health")

    assert re.fullmatch(r"[0-9a-f]{32}", response.headers["X-Trace-Id"])


async def test_request_id_header_is_echoed(client: httpx2.AsyncClient) -> None:
    valid = await client.get("/health", headers={"X-Request-Id": "abc-123"})
    invalid = await client.get("/health", headers={"X-Request-Id": "bad id!"})

    assert valid.headers["X-Request-Id"] == "abc-123"
    assert invalid.headers["X-Request-Id"] != "bad id!"
    assert re.fullmatch(r"[0-9a-f]{32}", invalid.headers["X-Request-Id"])


async def test_request_completed_log_carries_trace_id(
    client: httpx2.AsyncClient,
    auth_headers: dict[str, str],
    chat_payload: Callable[..., dict[str, Any]],
    log_capture: LogCapture,
) -> None:
    headers = auth_headers | {"traceparent": TRACEPARENT}

    await client.post("/v1/chat", json=chat_payload(), headers=headers)

    completed = [e for e in log_capture.entries if e["event"] == "request.completed"]
    assert len(completed) == 1
    assert completed[0]["trace_id"] == TRACE_ID
    assert completed[0]["status_code"] == 200
    assert completed[0]["path"] == "/v1/chat"
