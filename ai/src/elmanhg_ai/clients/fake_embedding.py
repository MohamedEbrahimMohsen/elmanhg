import hashlib
import math
import re
import unicodedata
from collections import deque
from collections.abc import Sequence
from typing import Final

from elmanhg_ai.clients.embedding import EmbeddingReply, EmbeddingRequest

FAKE_EMBEDDING_MODEL: Final = "fake-embedding"
TOKEN: Final = re.compile(r"\w+")
TASHKEEL: Final = range(0x064B, 0x0653)
TATWEEL: Final = 0x0640
ALEF: Final = 0x0627
ALEF_VARIANTS: Final = (0x0622, 0x0623, 0x0625)
ARABIC_FOLDING: Final = str.maketrans(
    {chr(code): None for code in (*TASHKEEL, TATWEEL)}
    | {chr(code): chr(ALEF) for code in ALEF_VARIANTS}
)


def fake_vector(text: str, dimensions: int) -> tuple[float, ...]:
    folded = unicodedata.normalize("NFKC", text).casefold().translate(ARABIC_FOLDING)
    vector = [0.0] * dimensions
    for token in TOKEN.findall(folded):
        digest = hashlib.blake2b(token.encode(), digest_size=8).digest()
        index = int.from_bytes(digest[:4], "big") % dimensions
        vector[index] += 1.0 if digest[4] & 1 == 0 else -1.0
    norm = math.sqrt(sum(value * value for value in vector))
    if norm == 0:
        return tuple(1.0 if index == 0 else 0.0 for index in range(dimensions))
    return tuple(value / norm for value in vector)


class FakeEmbeddingClient:
    def __init__(self, script: Sequence[EmbeddingReply | Exception] = ()) -> None:
        self._script: deque[EmbeddingReply | Exception] = deque(script)
        self.requests: list[EmbeddingRequest] = []

    async def embed(self, request: EmbeddingRequest) -> EmbeddingReply:
        self.requests.append(request)
        if self._script:
            step = self._script.popleft()
            if isinstance(step, Exception):
                raise step
            return step
        return EmbeddingReply(
            model=FAKE_EMBEDDING_MODEL,
            vectors=tuple(fake_vector(text, request.dimensions) for text in request.texts),
            input_tokens=0,
        )

    async def aclose(self) -> None:
        return None
