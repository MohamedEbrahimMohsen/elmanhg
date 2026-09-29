import re
from collections.abc import Callable
from typing import Any

import httpx2
import pytest

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration


async def test_unknown_route_returns_404_problem(client: httpx2.AsyncClient) -> None:
    response = await client.get("/nope")

    assert response.status_code == 404
    assert response.headers["content-type"].startswith("application/problem+json")
    body = response.json()
    assert body["code"] == "NOT_FOUND"
    assert body["instance"] == "/nope"
    assert body["type"] == "/problems/not-found"
    assert re.fullmatch(r"[0-9a-f]{32}", body["traceId"])


async def test_wrong_method_returns_405_problem(
    client: httpx2.AsyncClient, auth_headers: dict[str, str]
) -> None:
    response = await client.get("/v1/chat", headers=auth_headers)

    assert response.status_code == 405
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == "METHOD_NOT_ALLOWED"


async def test_unhandled_error_returns_500_problem_without_detail(
    settings: Settings,
    auth_headers: dict[str, str],
    chat_payload: Callable[..., dict[str, Any]],
) -> None:
    app = create_app(settings, model_client=FakeModelClient([RuntimeError("boom")]))
    transport = httpx2.ASGITransport(app=app, raise_app_exceptions=False)

    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        response = await client.post("/v1/chat", json=chat_payload(), headers=auth_headers)

    assert response.status_code == 500
    assert response.headers["content-type"].startswith("application/problem+json")
    body = response.json()
    assert body["code"] == "INTERNAL_ERROR"
    assert "detail" not in body
    assert "boom" not in response.text
