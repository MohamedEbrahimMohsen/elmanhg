from decimal import Decimal
from functools import lru_cache
from typing import Final, Literal, Self

from pydantic import Field, SecretStr, field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# Shared secret strength floor, same as the .NET validator.
MIN_SERVICE_TOKEN_LENGTH: Final = 32
# Every secret in the committed deploy/*.env.example files starts with this marker.
PLACEHOLDER_PREFIX: Final = "change-me"
OTLP_SCHEMES: Final = ("http://", "https://")


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_prefix="ELMANHG_AI_", extra="ignore", frozen=True, hide_input_in_errors=True
    )

    env: Literal["development", "testing", "production"] = "development"
    log_level: Literal["DEBUG", "INFO", "WARNING", "ERROR"] = "INFO"
    log_format: Literal["json", "console"] = "json"
    service_token: SecretStr
    llm_provider: Literal["fake", "anthropic"] = "fake"
    anthropic_api_key: SecretStr | None = None
    chat_model: str = Field(default="claude-sonnet-5", min_length=1)
    chat_prompt_version: str = Field(default="v2", pattern=r"^v[0-9]+$")
    chat_max_tokens: int = Field(default=1024, ge=1, le=8192)
    chat_max_history_messages: int = Field(default=20, ge=0, le=100)
    chat_max_message_chars: int = Field(default=4000, ge=1)
    chat_max_context_chars: int = Field(default=60000, ge=1)
    chat_max_sources: int = Field(default=20, ge=0, le=50)
    chat_max_source_chars: int = Field(default=8000, ge=1)
    essay_grading_model: str = Field(default="claude-sonnet-5", min_length=1)
    essay_grading_prompt_version: str = Field(default="v1", pattern=r"^v[0-9]+$")
    essay_grading_max_tokens: int = Field(default=2048, ge=1, le=8192)
    essay_grading_timeout_seconds: float = Field(default=45.0, gt=0, le=300)
    essay_grading_max_essay_chars: int = Field(default=20000, ge=1)
    essay_grading_max_field_chars: int = Field(default=20000, ge=1)
    essay_grading_max_criteria: int = Field(default=10, ge=1, le=50)
    essay_grading_max_model_answers: int = Field(default=3, ge=1, le=10)
    essay_grading_max_objectives: int = Field(default=20, ge=0, le=100)
    math_step_grading_model: str = Field(default="claude-sonnet-5", min_length=1)
    math_step_grading_prompt_version: str = Field(default="v1", pattern=r"^v[0-9]+$")
    math_step_grading_max_tokens: int = Field(default=2048, ge=1, le=8192)
    math_step_grading_timeout_seconds: float = Field(default=45.0, gt=0, le=300)
    math_step_grading_max_steps: int = Field(default=20, ge=1, le=100)
    math_step_grading_max_step_chars: int = Field(default=500, ge=1, le=5000)
    math_step_grading_max_accepted_answers: int = Field(default=20, ge=1, le=100)
    math_step_grading_max_field_chars: int = Field(default=20000, ge=1)
    math_step_grading_max_objectives: int = Field(default=20, ge=0, le=100)
    model_timeout_seconds: float = Field(default=20.0, gt=0, le=120)
    model_max_retries: int = Field(default=1, ge=0, le=5)
    model_input_usd_per_million_tokens: Decimal = Field(default=Decimal("3"), ge=0)
    model_output_usd_per_million_tokens: Decimal = Field(default=Decimal("15"), ge=0)
    embedding_provider: Literal["fake", "openai"] = "fake"
    openai_api_key: SecretStr | None = None
    embedding_model: str = Field(default="text-embedding-3-small", min_length=1)
    embedding_dimensions: int = Field(default=1536, ge=1, le=2000)
    embedding_max_texts: int = Field(default=64, ge=1, le=2048)
    embedding_max_text_chars: int = Field(default=8000, ge=1)
    embedding_usd_per_million_tokens: Decimal = Field(default=Decimal("0.02"), ge=0)
    transcription_provider: Literal["fake", "openai"] = "fake"
    transcription_model: str = Field(default="whisper-1", min_length=1)
    transcription_timeout_seconds: float = Field(default=60.0, gt=0, le=300)
    # OpenAI's 25 MB upload cap.
    transcription_max_audio_bytes: int = Field(default=10_485_760, ge=1, le=26_214_400)
    transcription_max_duration_seconds: int = Field(default=600, ge=1, le=3600)
    transcription_usd_per_minute: Decimal = Field(default=Decimal("0.006"), ge=0)
    otlp_endpoint: str | None = None
    otlp_headers: SecretStr | None = None
    otel_service_name: str = Field(default="elmanhg-ai", min_length=1)
    service_version: str = Field(default="dev", min_length=1)
    trace_sample_ratio: float = Field(default=1.0, ge=0, le=1)
    metric_export_interval_seconds: int = Field(default=30, ge=5, le=3600)
    cas_timeout_seconds: float = Field(default=5.0, gt=0, le=60)
    cas_workers: int = Field(default=2, ge=1, le=16)
    cas_worker_memory_mb: int = Field(default=1024, ge=256, le=8192)
    cas_max_tasks_per_worker: int = Field(default=200, ge=1, le=100000)
    cas_max_answer_chars: int = Field(default=500, ge=1, le=5000)
    cas_max_expected: int = Field(default=20, ge=1, le=100)
    cas_max_expected_chars: int = Field(default=500, ge=1, le=5000)
    cas_max_elements: int = Field(default=10, ge=1, le=50)
    cas_max_tokens: int = Field(default=300, ge=1, le=5000)
    cas_max_depth: int = Field(default=30, ge=1, le=200)
    cas_max_number_digits: int = Field(default=30, ge=1, le=1000)
    cas_max_exponent: int = Field(default=1000, ge=1, le=100000)
    cas_max_magnitude: int = Field(default=10000, ge=10, le=1000000)
    cas_max_expansion_terms: int = Field(default=500, ge=10, le=1_000_000)
    cas_warm_on_start: bool = True

    @field_validator("service_token")
    @classmethod
    def _service_token_long_enough(cls, value: SecretStr) -> SecretStr:
        if len(value.get_secret_value()) < MIN_SERVICE_TOKEN_LENGTH:
            raise ValueError("service_token must be at least 32 characters")
        return value

    @field_validator("otlp_endpoint")
    @classmethod
    def _otlp_endpoint_is_http(cls, value: str | None) -> str | None:
        endpoint = (value or "").strip()
        if not endpoint:
            return None
        if not endpoint.startswith(OTLP_SCHEMES):
            raise ValueError("otlp_endpoint must be an http or https URL")
        return endpoint

    @field_validator("otlp_headers")
    @classmethod
    def _otlp_headers_are_pairs(cls, value: SecretStr | None) -> SecretStr | None:
        headers = value.get_secret_value() if value is not None else ""
        if not headers.strip():
            return None
        for item in headers.split(","):
            key, separator, _ = item.partition("=")
            if not separator or not key.strip():
                raise ValueError("otlp_headers must be comma-separated key=value pairs")
        return value

    @model_validator(mode="after")
    def _anthropic_needs_key(self) -> Self:
        key = self.anthropic_api_key
        if self.llm_provider == "anthropic" and (key is None or not key.get_secret_value().strip()):
            raise ValueError("anthropic_api_key is required when llm_provider is anthropic")
        return self

    @model_validator(mode="after")
    def _openai_needs_key(self) -> Self:
        key = self.openai_api_key
        if self.embedding_provider == "openai" and (
            key is None or not key.get_secret_value().strip()
        ):
            raise ValueError("openai_api_key is required when embedding_provider is openai")
        return self

    @model_validator(mode="after")
    def _openai_transcription_needs_key(self) -> Self:
        key = self.openai_api_key
        if self.transcription_provider == "openai" and (
            key is None or not key.get_secret_value().strip()
        ):
            raise ValueError("openai_api_key is required when transcription_provider is openai")
        return self

    @model_validator(mode="after")
    def _production_token_is_not_placeholder(self) -> Self:
        token = self.service_token.get_secret_value()
        if self.env == "production" and token.startswith(PLACEHOLDER_PREFIX):
            raise ValueError("service_token still holds the example placeholder")
        return self


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
