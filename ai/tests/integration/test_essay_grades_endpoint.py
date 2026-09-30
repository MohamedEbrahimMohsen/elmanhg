import json
from collections.abc import Callable
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

Payload = Callable[..., dict[str, Any]]

GRADE_REPLY = ModelReply(
    text=json.dumps(
        {
            "criteria": [
                {"criterionId": "c1", "justification": "تعريف صحيح.", "points": 2},
                {"criterionId": "c2", "justification": "المثال ناقص.", "points": 1},
            ],
            "justification": "إجابة جيدة تحتاج إلى مثال أوضح.",
            "confidence": 0.82,
        },
        ensure_ascii=False,
    ),
    model="claude-sonnet-5",
    input_tokens=900,
    output_tokens=150,
    stop_reason="end_turn",
)


async def post_essay_grade(
    app: FastAPI, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post("/v1/essay-grades", json=payload, headers=headers)


def assert_problem(response: httpx2.Response, status_code: int, code: str) -> dict[str, Any]:
    assert response.status_code == status_code
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    assert body["code"] == code
    return body


async def test_create_essay_grade_returns_camel_case_grade(
    settings: Settings, auth_headers: dict[str, str], essay_payload: Payload
) -> None:
    app = create_app(settings, model_client=FakeModelClient([GRADE_REPLY]))

    response = await post_essay_grade(app, essay_payload(), auth_headers)

    assert response.status_code == 200
    body = response.json()
    assert body["criteria"][0] == {
        "criterionId": "c1",
        "points": 2,
        "justification": "تعريف صحيح.",
    }
    assert (body["totalPoints"], body["maxPoints"], body["promptVersion"]) == (3, 5, "v1")
    assert (body["confidence"], body["costUsd"]) == (0.82, 0.00495)


async def test_create_essay_grade_without_token_returns_401_problem(
    client: httpx2.AsyncClient, essay_payload: Payload, fake_model: FakeModelClient
) -> None:
    response = await client.post("/v1/essay-grades", json=essay_payload())

    assert_problem(response, 401, "UNAUTHENTICATED")
    assert fake_model.requests == []


async def test_create_essay_grade_invalid_body_returns_400_problem(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], essay_payload: Payload
) -> None:
    response = await client.post(
        "/v1/essay-grades", json=essay_payload(criteria=[]), headers=auth_headers
    )

    assert_problem(response, 400, "VALIDATION_FAILED")


async def test_create_essay_grade_default_fake_reply_returns_502_model_output_invalid(
    client: httpx2.AsyncClient, auth_headers: dict[str, str], essay_payload: Payload
) -> None:
    response = await client.post("/v1/essay-grades", json=essay_payload(), headers=auth_headers)

    assert_problem(response, 502, "MODEL_OUTPUT_INVALID")


async def test_create_essay_grade_model_unavailable_returns_503_problem(
    settings: Settings, auth_headers: dict[str, str], essay_payload: Payload
) -> None:
    app = create_app(settings, model_client=FakeModelClient([ModelUnavailableError()]))

    response = await post_essay_grade(app, essay_payload(), auth_headers)

    assert_problem(response, 503, "DEPENDENCY_UNAVAILABLE")
