import httpx2
import pytest
from fastapi import FastAPI

pytestmark = pytest.mark.integration


async def test_health_live_returns_ok(client: httpx2.AsyncClient) -> None:
    response = await client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


async def test_health_ready_after_startup_returns_ok(client: httpx2.AsyncClient) -> None:
    response = await client.get("/health/ready")

    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


async def test_health_ready_without_startup_returns_503_problem(app: FastAPI) -> None:
    transport = httpx2.ASGITransport(app=app)
    async with httpx2.AsyncClient(transport=transport, base_url="http://test") as client:
        response = await client.get("/health/ready")

    assert response.status_code == 503
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == "SERVICE_NOT_READY"


async def test_health_ready_without_essay_grading_prompts_returns_503(app: FastAPI) -> None:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        app.state.essay_grading_prompts = None
        response = await client.get("/health/ready")

    assert response.status_code == 503
    assert response.json()["code"] == "SERVICE_NOT_READY"
