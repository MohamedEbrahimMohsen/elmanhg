from dataclasses import dataclass
from decimal import Decimal
from typing import Literal, Protocol

from elmanhg_ai.clients.model import COST_QUANTUM, TOKENS_PER_MILLION
from elmanhg_ai.settings import Settings


@dataclass(frozen=True, slots=True)
class EmbeddingRequest:
    texts: tuple[str, ...]
    input_type: Literal["document", "query"]
    dimensions: int


@dataclass(frozen=True, slots=True)
class EmbeddingReply:
    model: str
    vectors: tuple[tuple[float, ...], ...]
    input_tokens: int


class EmbeddingClient(Protocol):
    async def embed(self, request: EmbeddingRequest) -> EmbeddingReply: ...

    async def aclose(self) -> None: ...


def estimate_embedding_cost_usd(input_tokens: int, settings: Settings) -> Decimal:
    return (input_tokens * settings.embedding_usd_per_million_tokens / TOKENS_PER_MILLION).quantize(
        COST_QUANTUM
    )


def build_embedding_client(settings: Settings) -> EmbeddingClient:
    from elmanhg_ai.clients.fake_embedding import FakeEmbeddingClient
    from elmanhg_ai.clients.openai_embedding import OpenAiEmbeddingClient

    match settings.embedding_provider:
        case "fake":
            return FakeEmbeddingClient()
        case "openai":
            return OpenAiEmbeddingClient.from_settings(settings)
