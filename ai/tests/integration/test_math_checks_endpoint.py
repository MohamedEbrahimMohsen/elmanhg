from typing import Any, Final

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

# A spawned worker imports SymPy first; this leaves room for a slow CI runner.
COLD_START_TIMEOUT_SECONDS: Final = 60.0


@pytest.fixture
def math_app(settings: Settings) -> FastAPI:
    return create_app(
        settings.model_copy(
            update={"cas_workers": 1, "cas_timeout_seconds": COLD_START_TIMEOUT_SECONDS}
        )
    )


async def post_math_check(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/math-checks", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> None:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == code


async def test_math_checks_equivalent_answer_returns_verdict(
    math_app: FastAPI, auth_headers: dict[str, str]
) -> None:
    payload = {"answer": "x=\\frac{4}{2}", "expected": ["x = 2"]}

    response = await post_math_check(math_app, payload, auth_headers)

    assert response.status_code == 200
    assert response.json() == {"verdict": "equivalent", "matchedIndex": 0, "invalidExpected": []}


async def test_math_checks_unreadable_answer_returns_200_unreadable(
    math_app: FastAPI, auth_headers: dict[str, str]
) -> None:
    response = await post_math_check(math_app, {"answer": "x +", "expected": ["2"]}, auth_headers)

    assert response.status_code == 200
    assert response.json()["verdict"] == "unreadable"


async def test_math_checks_missing_token_returns_401_problem(math_app: FastAPI) -> None:
    response = await post_math_check(math_app, {"answer": "2", "expected": ["2"]}, {})

    assert_problem(response, 401, "UNAUTHENTICATED")


async def test_math_checks_invalid_body_returns_400_problem(
    math_app: FastAPI, auth_headers: dict[str, str]
) -> None:
    payload = {"answer": "2", "expected": ["2"], "tolerance": 0.1}

    response = await post_math_check(math_app, payload, auth_headers)

    assert_problem(response, 400, "VALIDATION_FAILED")
