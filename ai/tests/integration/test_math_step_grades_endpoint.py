import json
from typing import Any

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelReply
from elmanhg_ai.core.errors import ModelUnavailableError
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

GRADE_REPLY = ModelReply(
    text=json.dumps(
        {
            "steps": [
                {"stepIndex": 0, "justification": "نقل الحد صحيح.", "points": 2},
                {"stepIndex": 1, "justification": "القسمة ناقصة.", "points": 1},
            ],
            "justification": "خطواتك سليمة، راجع القسمة.",
            "confidence": 0.82,
        },
        ensure_ascii=False,
    ),
    model="claude-sonnet-5",
    input_tokens=900,
    output_tokens=150,
    stop_reason="end_turn",
)


def _payload(**overrides: Any) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "question": "حل المعادلة 2x + 3 = 7",
        "modelSolution": ["2x = 4", "x = 2"],
        "acceptedAnswers": ["x = 2"],
        "steps": ["2x = 7 - 3", "x = 2"],
        "finalAnswer": "x = 2",
    }
    return payload | overrides


async def post_math_step_grade(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/math-step-grades", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> None:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    assert response.json()["code"] == code


async def test_create_math_step_grade_returns_camel_case_grade(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    app = create_app(settings, model_client=FakeModelClient([GRADE_REPLY]))

    response = await post_math_step_grade(app, _payload(), auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert body["steps"][0] == {"stepIndex": 0, "points": 2, "justification": "نقل الحد صحيح."}
    assert (body["totalPoints"], body["maxPoints"], body["promptVersion"]) == (3, 4, "v1")
    assert (body["confidence"], body["costUsd"]) == (0.82, 0.00495)


async def test_create_math_step_grade_without_token_returns_401_problem(
    client: httpx2.AsyncClient, fake_model: FakeModelClient
) -> None:
    response = await client.post("/v1/math-step-grades", json=_payload())

    assert_problem(response, 401, "UNAUTHENTICATED")
    assert fake_model.requests == []


async def test_create_math_step_grade_invalid_body_returns_400_problem(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], fake_model: FakeModelClient
) -> None:
    response = await client.post(
        "/v1/math-step-grades", json=_payload(modelSolution=[]), headers=auth_headers
    )

    assert_problem(response, 400, "VALIDATION_FAILED")
    assert fake_model.requests == []


async def test_create_math_step_grade_default_fake_reply_returns_502_model_output_invalid(
    client: httpx2.AsyncClient, auth_headers: dict[str, str]
) -> None:
    response = await client.post("/v1/math-step-grades", json=_payload(), headers=auth_headers)

    assert_problem(response, 502, "MODEL_OUTPUT_INVALID")


async def test_create_math_step_grade_model_unavailable_returns_503_problem(
    settings: Settings, auth_headers: dict[str, str]
) -> None:
    app = create_app(settings, model_client=FakeModelClient([ModelUnavailableError()]))

    response = await post_math_step_grade(app, _payload(), auth_headers)

    assert_problem(response, 503, "DEPENDENCY_UNAVAILABLE")
