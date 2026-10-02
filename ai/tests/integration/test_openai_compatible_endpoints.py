import json
from collections.abc import Callable
from typing import Any

import httpx2
import pytest
from fastapi import FastAPI

from elmanhg_ai.clients.openai_compatible_model import OpenAiCompatibleModelClient
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

Payload = Callable[..., dict[str, Any]]
Fixture = Callable[[str], str]

SOURCES = [
    {"reference": "explanation-1", "title": "الشرح — قانون أوم", "content": "V = I R"},
    {"reference": "explanation-2", "title": "الشرح — المقاومة", "content": "R = V / I"},
]


def model_client(
    body: str, captured: list[httpx2.Request], settings: Settings
) -> OpenAiCompatibleModelClient:
    def recording(request: httpx2.Request) -> httpx2.Response:
        captured.append(request)
        return httpx2.Response(200, content=body, headers={"content-type": "application/json"})

    http = httpx2.AsyncClient(
        base_url="https://llm.test/v1",
        headers={"Authorization": "Bearer test-key"},
        transport=httpx2.MockTransport(recording),
    )
    return OpenAiCompatibleModelClient(http, settings)


async def post(
    app: FastAPI, path: str, payload: dict[str, Any], headers: dict[str, str]
) -> httpx2.Response:
    transport = httpx2.ASGITransport(app=app)
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=transport, base_url="http://test") as client,
    ):
        return await client.post(path, json=payload, headers=headers)


async def test_chat_through_openai_compatible_client_returns_clean_reply_and_citations(
    settings: Settings,
    auth_headers: dict[str, str],
    chat_payload: Payload,
    openai_fixture: Fixture,
) -> None:
    captured: list[httpx2.Request] = []
    body = openai_fixture("chat_completion_with_citations.json")
    app = create_app(settings, model_client=model_client(body, captured, settings))

    response = await post(app, "/v1/chat", chat_payload(sources=SOURCES), auth_headers)

    assert response.status_code == 200
    reply = response.json()
    assert "[" not in reply["reply"]
    assert reply["citations"] == ["explanation-2", "explanation-1"]
    assert reply["promptVersion"] == "v3"
    assert "<lesson_sources>" in json.loads(captured[0].content)["messages"][-1]["content"]


async def test_essay_grade_through_openai_compatible_client_sends_schema_and_returns_grade(
    settings: Settings,
    auth_headers: dict[str, str],
    essay_payload: Payload,
    openai_fixture: Fixture,
) -> None:
    captured: list[httpx2.Request] = []
    body = openai_fixture("chat_completion_structured_grade.json")
    app = create_app(settings, model_client=model_client(body, captured, settings))

    response = await post(app, "/v1/essay-grades", essay_payload(), auth_headers)

    assert response.status_code == 200
    grade = response.json()
    assert (grade["totalPoints"], grade["confidence"]) == (3, 0.82)
    response_format = json.loads(captured[0].content)["response_format"]
    assert response_format["type"] == "json_schema"
    assert response_format["json_schema"]["schema"]["required"] == [
        "criteria",
        "justification",
        "confidence",
    ]
