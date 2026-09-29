import json
from pathlib import Path
from typing import Final

import pytest
from pydantic import SecretStr, ValidationError

from elmanhg_ai.clients.openai_transcription import OpenAiTranscriptionClient
from elmanhg_ai.clients.transcription import TranscriptionRequest
from elmanhg_ai.eval.transcription import WER_THRESHOLD, score_word_error_rate
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.eval

DATASET: Final = Path(__file__).parents[1] / "fixtures" / "transcription" / "eval"
MANIFEST: Final = DATASET / "manifest.jsonl"


async def test_eval_transcription_egyptian_dialect_mean_wer_within_threshold() -> None:
    if not MANIFEST.exists():
        pytest.skip("Egyptian-dialect clips not recorded yet (#96 deferral)")
    try:
        settings = Settings(service_token=SecretStr("x" * 32), transcription_provider="openai")
    except ValidationError:
        pytest.skip("needs ELMANHG_AI_OPENAI_API_KEY")
    cases = [json.loads(line) for line in MANIFEST.read_text(encoding="utf-8").splitlines() if line]
    client = OpenAiTranscriptionClient.from_settings(settings)
    scores: dict[str, float] = {}
    try:
        for case in cases:
            audio = (DATASET / case["audio"]).read_bytes()
            reply = await client.transcribe(TranscriptionRequest(audio, case["contentType"], "ar"))
            scores[case["audio"]] = score_word_error_rate(case["reference"], reply.text)
    finally:
        await client.aclose()

    mean = sum(scores.values()) / len(scores)
    assert mean <= WER_THRESHOLD, f"mean WER {mean:.3f} > {WER_THRESHOLD}; per clip {scores}"
