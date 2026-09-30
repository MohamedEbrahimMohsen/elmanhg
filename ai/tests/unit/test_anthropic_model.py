import dataclasses
import json
from collections.abc import Callable

import anthropic
import httpx2
import pytest
from pydantic import SecretStr

from elmanhg_ai.clients.anthropic_model import AnthropicModelClient
from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.clients.model import ModelMessage, ModelRequest, ModelSource, build_model_client
from elmanhg_ai.core.errors import ErrorCode, ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

Handler = Callable[[httpx2.Request], httpx2.Response]

REQUEST = ModelRequest(
    system="system prompt",
    messages=(
        ModelMessage(role="user", content="q1"),
        ModelMessage(role="assistant", content="a1"),
        ModelMessage(role="user", content="q2"),
    ),
    max_tokens=256,
)


def client_for(handler: Handler) -> AnthropicModelClient:
    sdk = anthropic.AsyncAnthropic(
        api_key="test-key",
        base_url="http://anthropic.test",
        max_retries=0,
        http_client=httpx2.AsyncClient(transport=httpx2.MockTransport(handler)),
    )
    return AnthropicModelClient(sdk, "claude-sonnet-5")


def respond_with(body: str, captured: list[httpx2.Request] | None = None) -> Handler:
    def handler(request: httpx2.Request) -> httpx2.Response:
        if captured is not None:
            captured.append(request)
        return httpx2.Response(200, content=body, headers={"content-type": "application/json"})

    return handler


async def test_anthropic_complete_sends_model_system_messages_and_max_tokens(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_success.json"), captured))

    await model.complete(REQUEST)

    sent = captured[0]
    body = json.loads(sent.content)
    assert sent.url.path == "/v1/messages"
    assert sent.headers["x-api-key"] == "test-key"
    assert body["model"] == "claude-sonnet-5"
    assert body["system"] == "system prompt"
    assert body["messages"] == [
        {"role": "user", "content": "q1"},
        {"role": "assistant", "content": "a1"},
        {"role": "user", "content": "q2"},
    ]
    assert body["max_tokens"] == 256
    assert "temperature" not in body


async def test_anthropic_complete_success_maps_text_usage_and_stop_reason(
    anthropic_fixture: Callable[[str], str],
) -> None:
    model = client_for(respond_with(anthropic_fixture("message_success.json")))

    reply = await model.complete(REQUEST)

    assert reply.text == "الخطوة ١: طبّق قانون أوم."
    assert reply.model == "claude-sonnet-5"
    assert (reply.input_tokens, reply.output_tokens) == (120, 40)
    assert reply.stop_reason == "end_turn"


async def test_anthropic_complete_server_error_raises_model_unavailable() -> None:
    def handler(request: httpx2.Request) -> httpx2.Response:
        return httpx2.Response(500, json={"type": "error", "error": {"type": "api_error"}})

    model = client_for(handler)

    with pytest.raises(ModelUnavailableError) as error:
        await model.complete(REQUEST)

    assert error.value.code == ErrorCode.DEPENDENCY_UNAVAILABLE


async def test_anthropic_complete_timeout_raises_model_unavailable() -> None:
    def handler(request: httpx2.Request) -> httpx2.Response:
        raise httpx2.ReadTimeout("timed out", request=request)

    model = client_for(handler)

    with pytest.raises(ModelUnavailableError):
        await model.complete(REQUEST)


async def test_anthropic_complete_no_text_block_raises_model_output_invalid(
    anthropic_fixture: Callable[[str], str],
) -> None:
    model = client_for(respond_with(anthropic_fixture("message_no_text.json")))

    with pytest.raises(ModelOutputInvalidError) as error:
        await model.complete(REQUEST)

    assert error.value.code == ErrorCode.MODEL_OUTPUT_INVALID


def test_anthropic_from_settings_without_api_key_raises_value_error(settings: Settings) -> None:
    keyless = settings.model_copy(update={"llm_provider": "anthropic", "anthropic_api_key": None})

    with pytest.raises(ValueError, match=r"^anthropic_api_key is required") as error:
        AnthropicModelClient.from_settings(keyless)

    assert str(error.value) == "anthropic_api_key is required when llm_provider is anthropic"


async def test_build_model_client_anthropic_provider_returns_anthropic_client(
    settings: Settings,
) -> None:
    anthropic_settings = settings.model_copy(
        update={"llm_provider": "anthropic", "anthropic_api_key": SecretStr("test-key")}
    )

    anthropic_client = build_model_client(anthropic_settings)
    fake_client = build_model_client(settings)

    assert isinstance(anthropic_client, AnthropicModelClient)
    assert isinstance(fake_client, FakeModelClient)
    await anthropic_client.aclose()


async def test_anthropic_complete_with_sources_sends_search_result_blocks_before_turn(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_success.json"), captured))
    request = ModelRequest(
        system=REQUEST.system,
        messages=REQUEST.messages,
        max_tokens=REQUEST.max_tokens,
        sources=(
            ModelSource(reference="explanation-1", title="الشرح — قانون أوم", content="V = I R"),
        ),
    )

    await model.complete(request)

    body = json.loads(captured[0].content)
    assert body["messages"][:2] == [
        {"role": "user", "content": "q1"},
        {"role": "assistant", "content": "a1"},
    ]
    assert body["messages"][2] == {
        "role": "user",
        "content": [
            {
                "type": "search_result",
                "source": "explanation-1",
                "title": "الشرح — قانون أوم",
                "content": [{"type": "text", "text": "V = I R"}],
                "citations": {"enabled": True},
            },
            {"type": "text", "text": "q2"},
        ],
    }


async def test_anthropic_complete_with_citations_maps_sources_in_order_distinct(
    anthropic_fixture: Callable[[str], str],
) -> None:
    model = client_for(respond_with(anthropic_fixture("message_with_citations.json")))

    reply = await model.complete(REQUEST)

    assert reply.citations == ("explanation-2", "explanation-1")
    assert reply.text == "1. المقاومة = فرق الجهد ÷ شدة التيار. 2. إذن R = 4 أوم."


async def test_anthropic_complete_with_output_schema_sends_output_config_json_schema(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_structured_grade.json"), captured))
    schema = '{"type":"object","additionalProperties":false,"properties":{}}'

    await model.complete(dataclasses.replace(REQUEST, output_schema=schema))

    body = json.loads(captured[0].content)
    assert body["output_config"]["format"] == {"type": "json_schema", "schema": json.loads(schema)}


async def test_anthropic_complete_without_output_schema_omits_output_config(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_success.json"), captured))

    await model.complete(REQUEST)

    assert "output_config" not in json.loads(captured[0].content)


async def test_anthropic_complete_with_model_override_sends_that_model(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_success.json"), captured))

    await model.complete(dataclasses.replace(REQUEST, model="claude-opus-5"))

    assert json.loads(captured[0].content)["model"] == "claude-opus-5"


async def test_anthropic_complete_with_timeout_sets_request_read_timeout(
    anthropic_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    model = client_for(respond_with(anthropic_fixture("message_success.json"), captured))

    await model.complete(dataclasses.replace(REQUEST, timeout_seconds=45.0))

    assert captured[0].extensions["timeout"]["read"] == 45.0


async def test_anthropic_complete_structured_reply_returns_json_text(
    anthropic_fixture: Callable[[str], str],
) -> None:
    model = client_for(respond_with(anthropic_fixture("message_structured_grade.json")))

    reply = await model.complete(REQUEST)

    assert json.loads(reply.text)["confidence"] == 0.82
    assert (reply.input_tokens, reply.output_tokens) == (900, 150)
