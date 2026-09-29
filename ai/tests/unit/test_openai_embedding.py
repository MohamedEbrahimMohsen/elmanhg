import json
from collections.abc import Callable

import httpx2
import pytest
from pydantic import SecretStr

from elmanhg_ai.clients.embedding import EmbeddingClient, EmbeddingRequest, build_embedding_client
from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
from elmanhg_ai.clients.openai_embedding import OPENAI_BASE_URL, OpenAiEmbeddingClient
from elmanhg_ai.core.errors import ErrorCode, ModelOutputInvalidError, ModelUnavailableError
from elmanhg_ai.settings import Settings

Handler = Callable[[httpx2.Request], httpx2.Response]

REQUEST = EmbeddingRequest(texts=("first", "second"), input_type="document", dimensions=3)
MAX_RETRIES = 2


class RecordingSleep:
    def __init__(self) -> None:
        self.delays: list[float] = []

    async def __call__(self, delay: float) -> None:
        self.delays.append(delay)


def client_for(
    handler: Handler, captured: list[httpx2.Request], sleep: RecordingSleep | None = None
) -> OpenAiEmbeddingClient:
    def recording(request: httpx2.Request) -> httpx2.Response:
        captured.append(request)
        return handler(request)

    http = httpx2.AsyncClient(
        base_url=OPENAI_BASE_URL,
        headers={"Authorization": "Bearer test-key"},
        transport=httpx2.MockTransport(recording),
    )
    return OpenAiEmbeddingClient(
        http, "text-embedding-3-small", MAX_RETRIES, sleep or RecordingSleep()
    )


def respond(status_code: int, body: str = "{}") -> httpx2.Response:
    return httpx2.Response(status_code, content=body, headers={"content-type": "application/json"})


async def test_openai_embed_sends_model_input_dimensions_and_bearer(
    openai_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    client = client_for(lambda _: respond(200, openai_fixture("embeddings_success.json")), captured)

    await client.embed(REQUEST)

    sent = captured[0]
    assert sent.url.path == "/v1/embeddings"
    assert sent.headers["Authorization"] == "Bearer test-key"
    assert json.loads(sent.content) == {
        "model": "text-embedding-3-small",
        "input": ["first", "second"],
        "dimensions": 3,
        "encoding_format": "float",
    }


async def test_openai_embed_success_orders_vectors_by_index_and_maps_usage(
    openai_fixture: Callable[[str], str],
) -> None:
    client = client_for(lambda _: respond(200, openai_fixture("embeddings_success.json")), [])

    reply = await client.embed(REQUEST)

    assert reply.vectors == ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    assert reply.input_tokens == 7
    assert reply.model == "text-embedding-3-small"


async def test_openai_embed_rate_limited_then_success_retries_once(
    openai_fixture: Callable[[str], str],
) -> None:
    captured: list[httpx2.Request] = []
    sleep = RecordingSleep()
    responses = iter([respond(429), respond(200, openai_fixture("embeddings_success.json"))])
    client = client_for(lambda _: next(responses), captured, sleep)

    reply = await client.embed(REQUEST)

    assert len(captured) == 2
    assert sleep.delays == [0.5]
    assert reply.input_tokens == 7


async def test_openai_embed_server_error_exhausts_retries_raises_model_unavailable() -> None:
    captured: list[httpx2.Request] = []
    sleep = RecordingSleep()
    client = client_for(lambda _: respond(503), captured, sleep)

    with pytest.raises(ModelUnavailableError) as error:
        await client.embed(REQUEST)

    assert error.value.code == ErrorCode.DEPENDENCY_UNAVAILABLE
    assert len(captured) == MAX_RETRIES + 1
    assert sleep.delays == [0.5, 1.0]


async def test_openai_embed_bad_request_raises_without_retry() -> None:
    captured: list[httpx2.Request] = []
    client = client_for(lambda _: respond(400), captured)

    with pytest.raises(ModelUnavailableError):
        await client.embed(REQUEST)

    assert len(captured) == 1


async def test_openai_embed_transport_error_raises_model_unavailable() -> None:
    def handler(request: httpx2.Request) -> httpx2.Response:
        raise httpx2.ConnectError("refused", request=request)

    captured: list[httpx2.Request] = []
    client = client_for(handler, captured)

    with pytest.raises(ModelUnavailableError):
        await client.embed(REQUEST)

    assert len(captured) == MAX_RETRIES + 1


async def test_openai_embed_malformed_body_raises_model_output_invalid() -> None:
    client = client_for(lambda _: respond(200, '{"data": [{"index": 0}]}'), [])

    with pytest.raises(ModelOutputInvalidError) as error:
        await client.embed(REQUEST)

    assert error.value.code == ErrorCode.MODEL_OUTPUT_INVALID


@pytest.mark.parametrize(
    ("provider", "expected"),
    [("fake", FakeEmbeddingClient), ("openai", OpenAiEmbeddingClient)],
    ids=["fake", "openai"],
)
async def test_build_embedding_client_selects_provider(
    settings: Settings, provider: str, expected: type[EmbeddingClient]
) -> None:
    configured = settings.model_copy(
        update={"embedding_provider": provider, "openai_api_key": SecretStr("test-key")}
    )

    client = build_embedding_client(configured)

    assert isinstance(client, expected)
    await client.aclose()
