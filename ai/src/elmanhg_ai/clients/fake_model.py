from collections import deque
from collections.abc import Sequence
from typing import Final

from elmanhg_ai.clients.model import ModelReply, ModelRequest

FAKE_MODEL: Final = "fake"
FAKE_REPLY: Final = "هذا رد تجريبي من المساعد."


class FakeModelClient:
    def __init__(self, script: Sequence[ModelReply | Exception] = ()) -> None:
        self._script: deque[ModelReply | Exception] = deque(script)
        self.requests: list[ModelRequest] = []

    async def complete(self, request: ModelRequest) -> ModelReply:
        self.requests.append(request)
        if self._script:
            step = self._script.popleft()
            if isinstance(step, Exception):
                raise step
            return step
        return ModelReply(
            text=FAKE_REPLY,
            model=FAKE_MODEL,
            input_tokens=0,
            output_tokens=0,
            stop_reason="end_turn",
        )

    async def aclose(self) -> None:
        return None
