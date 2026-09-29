from collections import deque
from collections.abc import Sequence
from typing import Final

from elmanhg_ai.clients.transcription import TranscriptionReply, TranscriptionRequest

FAKE_TRANSCRIPTION_MODEL: Final = "fake-transcription"
FAKE_TRANSCRIPT: Final = "هذا تفريغ تجريبي للرد الصوتي."


class FakeTranscriptionClient:
    def __init__(self, script: Sequence[TranscriptionReply | Exception] = ()) -> None:
        self._script: deque[TranscriptionReply | Exception] = deque(script)
        self.requests: list[TranscriptionRequest] = []

    async def transcribe(self, request: TranscriptionRequest) -> TranscriptionReply:
        self.requests.append(request)
        if self._script:
            step = self._script.popleft()
            if isinstance(step, Exception):
                raise step
            return step
        return TranscriptionReply(text=FAKE_TRANSCRIPT, model=FAKE_TRANSCRIPTION_MODEL)

    async def aclose(self) -> None:
        return None
