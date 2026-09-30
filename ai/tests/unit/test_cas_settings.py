from typing import Any, Final

import pytest
from pydantic import SecretStr, ValidationError

from elmanhg_ai.settings import Settings

VALID_TOKEN: Final = "elmanhg-tests-ai-service-token-0123456789"


def test_settings_cas_defaults() -> None:
    settings = Settings(service_token=SecretStr(VALID_TOKEN))

    assert (settings.cas_timeout_seconds, settings.cas_workers) == (5.0, 2)
    assert (settings.cas_worker_memory_mb, settings.cas_max_tasks_per_worker) == (1024, 200)
    assert (settings.cas_max_answer_chars, settings.cas_max_expected) == (500, 20)
    assert (settings.cas_max_expected_chars, settings.cas_max_elements) == (500, 10)
    assert (settings.cas_max_tokens, settings.cas_max_depth) == (300, 30)
    assert (settings.cas_max_number_digits, settings.cas_max_exponent) == (30, 1000)
    assert settings.cas_max_magnitude == 10000


@pytest.mark.parametrize(
    "overrides",
    [
        {"cas_timeout_seconds": 0},
        {"cas_workers": 0},
        {"cas_worker_memory_mb": 100},
        {"cas_max_magnitude": 1},
    ],
    ids=["zero-timeout", "no-workers", "too-little-memory", "tiny-magnitude"],
)
def test_settings_cas_out_of_range_rejected(overrides: dict[str, Any]) -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), **overrides)

    assert error.value.errors()[0]["loc"] == (next(iter(overrides)),)
