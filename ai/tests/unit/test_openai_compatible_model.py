import dataclasses
import json
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any

import httpx2
import pytest
import structlog
from pydantic import SecretStr

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelMessage, ModelRequest, ModelSource, build_model_client
from elmanhg_ai.clients.openai_compatible_model import (
    MISSING_KEY,
    OpenAiCompatibleModelClient,
    api_key_for,
)
from elmanhg_ai.core.errors import ErrorCode, ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

Handler = Callable[[httpx2.Request], httpx2.Response]

SUCCESS = "chat_completion_success.json"
CITED = "chat_completion_with_citations.json"
GRADE = "chat_completion_structured_grade.json"
REQUEST = ModelRequest(
    system="system prompt",
    messages=(
        ModelMessage(role="user", content="q1"),
        ModelMessage(role="assistant", content="a1"),
        ModelMessage(role="user", content="q2"),
    ),
    max_tokens=256,
)
SCHEMA = '{"type":"object","properties":{},"required":[],"additionalProperties":false}'
STRUCTURED = dataclasses.replace(REQUEST, output_schema=SCHEMA)
SENT = [
    {"role": "system", "content": "system prompt"},
    {"role": "user", "content": "q1"},
    {"role": "assistant", "content": "a1"},
    {"role": "user", "content": "q2"},
]
SOURCES = (
    ModelSource(reference="explanation-1", title="الشرح — قانون أوم", content="V = I R"),
    ModelSource(reference="explanation-2", title="الشرح — المقاومة", content="R = V / I"),
)


class RecordingSleep:
    def __init__(self) -> None:
        self.delays: list[float] = []

    async def __call__(self, delay: float) -> None:
        self.delays.append(delay)


def respond(status_code: int, body: str = "{}") -> httpx2.Response:
    return httpx2.Response(status_code, content=body, headers={"content-type": "application/json"})


@dataclass
class Llm:
    settings: Settings
    read: Callable[[str], str]
    captured: list[httpx2.Request] = field(default_factory=list)
    sleep: RecordingSleep = field(default_factory=RecordingSleep)

    def client(self, reply: str | Handler = SUCCESS, **update: Any) -> OpenAiCompatibleModelClient:
        body = "" if callable(reply) else self.read(reply)
        handler = reply if callable(reply) else lambda _: respond(200, body)

        def recording(request: httpx2.Request) -> httpx2.Response:
            self.captured.append(request)
            return handler(request)

        http = httpx2.AsyncClient(
            base_url="https://llm.test/v1",
            headers={"Authorization": "Bearer test-key"},
            transport=httpx2.MockTransport(recording),
        )
        settings = self.settings.model_copy(update={"model_max_retries": 2, **update})
        return OpenAiCompatibleModelClient(http, settings, self.sleep)

    def body(self) -> dict[str, Any]:
        return dict(json.loads(self.captured[0].content))


@pytest.fixture
def llm(settings: Settings, openai_fixture: Callable[[str], str]) -> Llm:
    return Llm(settings, openai_fixture)


async def test_complete_sends_path_bearer_model_system_messages_and_max_completion_tokens(
    llm: Llm,
) -> None:
    await llm.client().complete(REQUEST)

    body = llm.body()
    assert llm.captured[0].url.path == "/v1/chat/completions"
    assert llm.captured[0].headers["Authorization"] == "Bearer test-key"
    assert body["model"] == llm.settings.chat_model
    assert body["messages"] == SENT
    assert body["max_completion_tokens"] == 256
    assert {"max_tokens", "temperature", "response_format"}.isdisjoint(body)
    assert body["reasoning_effort"] == "low"


async def test_complete_success_maps_text_model_usage_and_finish_reason(llm: Llm) -> None:
    reply = await llm.client().complete(REQUEST)

    assert (reply.text, reply.model) == ("الخطوة ١: طبّق قانون أوم.", "gpt-5.6-luna")
    assert (reply.input_tokens, reply.output_tokens) == (120, 40)
    assert (reply.stop_reason, reply.citations) == ("stop", ())


async def test_complete_max_tokens_field_override_sends_max_tokens(llm: Llm) -> None:
    await llm.client(llm_max_tokens_field="max_tokens").complete(REQUEST)

    assert {k: v for k, v in llm.body().items() if "tokens" in k} == {"max_tokens": 256}


async def test_complete_reasoning_effort_default_omits_field(llm: Llm) -> None:
    await llm.client(llm_reasoning_effort="default").complete(REQUEST)

    assert "reasoning_effort" not in llm.body()


async def test_complete_with_model_override_sends_that_model(llm: Llm) -> None:
    await llm.client().complete(dataclasses.replace(REQUEST, model="deepseek-v4-flash"))

    assert llm.body()["model"] == "deepseek-v4-flash"


async def test_complete_with_timeout_sets_request_read_timeout(llm: Llm) -> None:
    await llm.client().complete(dataclasses.replace(REQUEST, timeout_seconds=45.0))

    assert llm.captured[0].extensions["timeout"]["read"] == 45.0


async def test_complete_without_timeout_uses_model_timeout_seconds(llm: Llm) -> None:
    await llm.client().complete(REQUEST)

    assert llm.captured[0].extensions["timeout"]["read"] == llm.settings.model_timeout_seconds


async def test_complete_with_output_schema_sends_strict_json_schema_response_format(
    llm: Llm,
) -> None:
    await llm.client(GRADE).complete(STRUCTURED)

    body = llm.body()
    assert body["response_format"] == {
        "type": "json_schema",
        "json_schema": {"name": "structured_output", "strict": True, "schema": json.loads(SCHEMA)},
    }
    assert body["messages"][0] == SENT[0]


async def test_complete_json_object_mode_sends_json_object_and_appends_schema_to_system(
    llm: Llm,
) -> None:
    await llm.client(GRADE, llm_structured_output="json_object").complete(STRUCTURED)

    system = llm.body()["messages"][0]["content"]
    assert llm.body()["response_format"] == {"type": "json_object"}
    assert system.startswith("system prompt\n\n")
    assert SCHEMA in system
    assert "JSON" in system


async def test_complete_structured_reply_returns_json_text(llm: Llm) -> None:
    reply = await llm.client(GRADE).complete(STRUCTURED)

    assert json.loads(reply.text)["confidence"] == 0.82
    assert (reply.input_tokens, reply.output_tokens) == (900, 150)


async def test_complete_with_sources_wraps_last_turn_in_lesson_sources_block(llm: Llm) -> None:
    await llm.client().complete(dataclasses.replace(REQUEST, sources=SOURCES[:1]))

    messages = llm.body()["messages"]
    content = messages[3]["content"]
    block = content.split("<lesson_sources>\n")[1].split("\n</lesson_sources>")[0]
    assert messages[:3] == SENT[:3]
    assert content.startswith("<lesson_sources>\n")
    assert json.loads(block) == [
        {"reference": "explanation-1", "title": "الشرح — قانون أوم", "content": "V = I R"}
    ]
    assert content.rstrip().endswith("q2")


async def test_complete_with_citations_strips_markers_and_returns_supplied_references(
    llm: Llm,
) -> None:
    reply = await llm.client(CITED).complete(dataclasses.replace(REQUEST, sources=SOURCES))

    assert reply.text == "1. المقاومة = فرق الجهد ÷ شدة التيار.\n2. إذن R = 4 أوم."
    assert reply.citations == ("explanation-2", "explanation-1")


async def test_complete_without_sources_leaves_brackets_and_returns_no_citations(
    llm: Llm,
) -> None:
    reply = await llm.client(CITED).complete(REQUEST)

    assert "[explanation-2]" in reply.text
    assert reply.citations == ()


async def test_complete_rate_limited_then_success_retries_once(llm: Llm) -> None:
    responses = iter([respond(429), respond(200, llm.read(SUCCESS))])

    reply = await llm.client(lambda _: next(responses)).complete(REQUEST)

    assert (len(llm.captured), llm.sleep.delays) == (2, [0.5])
    assert reply.stop_reason == "stop"


async def test_complete_server_error_exhausts_retries_raises_model_unavailable(
    llm: Llm,
) -> None:
    with pytest.raises(ModelUnavailableError) as error:
        await llm.client(lambda _: respond(503)).complete(REQUEST)

    assert error.value.code == ErrorCode.DEPENDENCY_UNAVAILABLE
    assert (len(llm.captured), llm.sleep.delays) == (3, [0.5, 1.0])


async def test_complete_bad_request_raises_without_retry(llm: Llm) -> None:
    with pytest.raises(ModelUnavailableError):
        await llm.client(lambda _: respond(400)).complete(REQUEST)

    assert len(llm.captured) == 1


async def test_complete_read_timeout_raises_model_unavailable(llm: Llm) -> None:
    def handler(request: httpx2.Request) -> httpx2.Response:
        raise httpx2.ReadTimeout("timed out", request=request)

    with pytest.raises(ModelUnavailableError):
        await llm.client(handler).complete(REQUEST)


async def test_complete_failure_logs_call_failed_with_openai_compatible_provider(
    llm: Llm,
) -> None:
    client = llm.client(lambda _: respond(400))

    with structlog.testing.capture_logs() as logs, pytest.raises(ModelUnavailableError):
        await client.complete(REQUEST)

    failed = [entry for entry in logs if entry["event"] == "model.call_failed"]
    assert failed[0]["provider"] == "openai_compatible"
    assert failed[0]["status_code"] == 400
    assert all("test-key" not in str(value) for entry in logs for value in entry.values())


async def test_complete_empty_content_raises_model_output_invalid(llm: Llm) -> None:
    with pytest.raises(ModelOutputInvalidError) as error:
        await llm.client("chat_completion_empty_content.json").complete(REQUEST)

    assert error.value.code == ErrorCode.MODEL_OUTPUT_INVALID


async def test_complete_unreadable_body_raises_model_output_invalid(llm: Llm) -> None:
    with pytest.raises(ModelOutputInvalidError) as error:
        await llm.client(lambda _: respond(200, "{}")).complete(REQUEST)

    assert error.value.code == ErrorCode.MODEL_OUTPUT_INVALID


def test_api_key_for_prefers_llm_key_then_openai_key_else_raises(settings: Settings) -> None:
    both = settings.model_copy(
        update={"llm_api_key": SecretStr("llm-key"), "openai_api_key": SecretStr("openai-key")}
    )
    openai_only = both.model_copy(update={"llm_api_key": None})
    neither = openai_only.model_copy(update={"openai_api_key": None})

    assert api_key_for(both).get_secret_value() == "llm-key"
    assert api_key_for(openai_only).get_secret_value() == "openai-key"
    with pytest.raises(ValueError, match="openai_compatible") as error:
        api_key_for(neither)
    assert str(error.value) == MISSING_KEY


async def test_build_model_client_openai_compatible_returns_openai_compatible_client(
    settings: Settings,
) -> None:
    configured = settings.model_copy(
        update={"llm_provider": "openai_compatible", "openai_api_key": SecretStr("test-key")}
    )

    real = build_model_client(configured)
    fake = build_model_client(settings)

    assert isinstance(real, OpenAiCompatibleModelClient)
    assert isinstance(fake, FakeModelClient)
    await real.aclose()
