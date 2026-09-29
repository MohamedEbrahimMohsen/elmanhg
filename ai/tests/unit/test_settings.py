import pytest
from pydantic import SecretStr, ValidationError

from elmanhg_ai.settings import Settings

VALID_TOKEN = "a" * 32
SECRET_TOKEN = "real-secret-QZX-service-token-0123456789"


def test_settings_defaults_select_fake_provider_and_v2_prompt() -> None:
    settings = Settings(service_token=SecretStr(VALID_TOKEN))

    assert settings.llm_provider == "fake"
    assert settings.chat_model == "claude-sonnet-5"
    assert settings.chat_prompt_version == "v2"
    assert settings.chat_max_tokens == 1024
    assert settings.chat_max_sources == 20
    assert settings.chat_max_source_chars == 8000


def test_settings_short_service_token_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr("a" * 31))

    assert error.value.errors()[0]["loc"] == ("service_token",)


def test_settings_anthropic_without_api_key_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), llm_provider="anthropic")

    assert "anthropic_api_key is required" in str(error.value)


def test_settings_short_service_token_error_hides_token_value(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("ELMANHG_AI_SERVICE_TOKEN", "short-secret-ABC")

    with pytest.raises(ValidationError) as error:
        Settings()

    message = str(error.value)
    assert "service_token must be at least 32 characters" in message
    assert "short-secret-ABC" not in message
    assert "short-s" not in message


def test_settings_anthropic_without_api_key_error_hides_token_value(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("ELMANHG_AI_SERVICE_TOKEN", SECRET_TOKEN)
    monkeypatch.setenv("ELMANHG_AI_LLM_PROVIDER", "anthropic")
    monkeypatch.delenv("ELMANHG_AI_ANTHROPIC_API_KEY", raising=False)

    with pytest.raises(ValidationError) as error:
        Settings()

    message = str(error.value)
    assert "anthropic_api_key is required" in message
    assert SECRET_TOKEN not in message
    assert SECRET_TOKEN[:6] not in message
    assert SECRET_TOKEN[-6:] not in message


def test_settings_invalid_prompt_version_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), chat_prompt_version="version1")

    assert error.value.errors()[0]["loc"] == ("chat_prompt_version",)


def test_settings_reads_prefixed_environment_variables(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("ELMANHG_AI_SERVICE_TOKEN", VALID_TOKEN)
    monkeypatch.setenv("ELMANHG_AI_CHAT_MODEL", "claude-haiku-4-5")

    settings = Settings()

    assert settings.service_token.get_secret_value() == VALID_TOKEN
    assert settings.chat_model == "claude-haiku-4-5"


def test_settings_defaults_select_fake_embeddings_1536() -> None:
    settings = Settings(service_token=SecretStr(VALID_TOKEN))

    assert settings.embedding_provider == "fake"
    assert settings.embedding_model == "text-embedding-3-small"
    assert settings.embedding_dimensions == 1536
    assert settings.embedding_max_texts == 64


def test_settings_openai_without_api_key_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), embedding_provider="openai")

    assert "openai_api_key is required" in str(error.value)


def test_settings_embedding_dimensions_above_2000_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), embedding_dimensions=2001)

    assert error.value.errors()[0]["loc"] == ("embedding_dimensions",)


def test_settings_defaults_select_fake_transcription_whisper() -> None:
    settings = Settings(service_token=SecretStr(VALID_TOKEN))

    assert settings.transcription_provider == "fake"
    assert settings.transcription_model == "whisper-1"
    assert settings.transcription_timeout_seconds == 60
    assert settings.transcription_max_audio_bytes == 10_485_760
    assert settings.transcription_max_duration_seconds == 600


def test_settings_openai_transcription_without_api_key_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), transcription_provider="openai")

    assert "openai_api_key is required when transcription_provider is openai" in str(error.value)


def test_settings_transcription_max_audio_bytes_above_25mb_raises_validation_error() -> None:
    with pytest.raises(ValidationError) as error:
        Settings(service_token=SecretStr(VALID_TOKEN), transcription_max_audio_bytes=26_214_401)

    assert error.value.errors()[0]["loc"] == ("transcription_max_audio_bytes",)
