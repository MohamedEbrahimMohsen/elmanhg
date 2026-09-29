import pytest

from elmanhg_ai.clients.fake_model import FAKE_REPLY, FakeModelClient
from elmanhg_ai.clients.model import ModelMessage, ModelRequest, ModelSource
from elmanhg_ai.core.errors import ModelUnavailableError

REQUEST = ModelRequest(
    system="system", messages=(ModelMessage(role="user", content="hi"),), max_tokens=10
)


async def test_fake_model_default_returns_fixed_reply_and_records_request() -> None:
    model = FakeModelClient()

    reply = await model.complete(REQUEST)

    assert reply.text == FAKE_REPLY
    assert reply.model == "fake"
    assert model.requests == [REQUEST]


async def test_fake_model_scripted_error_raises_it() -> None:
    model = FakeModelClient([ModelUnavailableError()])

    with pytest.raises(ModelUnavailableError):
        await model.complete(REQUEST)


async def test_fake_model_with_sources_cites_first_source() -> None:
    model = FakeModelClient()
    sources = (
        ModelSource(reference="explanation-1", title="الشرح", content="V = I R"),
        ModelSource(reference="summary-1", title="الملخص", content="R = V / I"),
    )

    reply = await model.complete(
        ModelRequest(system="system", messages=REQUEST.messages, max_tokens=10, sources=sources)
    )

    assert reply.citations == ("explanation-1",)
