from dataclasses import dataclass
from decimal import Decimal
from typing import Final, Literal, Protocol

from elmanhg_ai.settings import Settings

TOKENS_PER_MILLION: Final = Decimal(1_000_000)
COST_QUANTUM: Final = Decimal("0.000001")


@dataclass(frozen=True, slots=True)
class ModelMessage:
    role: Literal["user", "assistant"]
    content: str


@dataclass(frozen=True, slots=True)
class ModelSource:
    reference: str
    title: str
    content: str


@dataclass(frozen=True, slots=True)
class ModelRequest:
    system: str
    messages: tuple[ModelMessage, ...]
    max_tokens: int
    sources: tuple[ModelSource, ...] = ()
    model: str | None = None
    timeout_seconds: float | None = None
    output_schema: str | None = None


@dataclass(frozen=True, slots=True)
class ModelReply:
    text: str
    model: str
    input_tokens: int
    output_tokens: int
    stop_reason: str | None
    citations: tuple[str, ...] = ()


class ModelClient(Protocol):
    async def complete(self, request: ModelRequest) -> ModelReply: ...

    async def aclose(self) -> None: ...


def estimate_cost_usd(input_tokens: int, output_tokens: int, settings: Settings) -> Decimal:
    return (
        (
            input_tokens * settings.model_input_usd_per_million_tokens
            + output_tokens * settings.model_output_usd_per_million_tokens
        )
        / TOKENS_PER_MILLION
    ).quantize(COST_QUANTUM)


def build_model_client(settings: Settings) -> ModelClient:
    from elmanhg_ai.clients.fake_model import FakeModelClient
    from elmanhg_ai.clients.openai_compatible_model import OpenAiCompatibleModelClient

    match settings.llm_provider:
        case "fake":
            return FakeModelClient()
        case "openai_compatible":
            return OpenAiCompatibleModelClient.from_settings(settings)
