from decimal import Decimal
from functools import lru_cache
from typing import Final, Literal, Self

from pydantic import Field, SecretStr, field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

# Shared secret strength floor, same as the .NET validator.
MIN_SERVICE_TOKEN_LENGTH: Final = 32


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
    chat_prompt_version: str = Field(default="v1", pattern=r"^v[0-9]+$")
    chat_max_tokens: int = Field(default=1024, ge=1, le=8192)
    chat_max_history_messages: int = Field(default=20, ge=0, le=100)
    chat_max_message_chars: int = Field(default=4000, ge=1)
    chat_max_context_chars: int = Field(default=60000, ge=1)
    model_timeout_seconds: float = Field(default=20.0, gt=0, le=120)
    model_max_retries: int = Field(default=1, ge=0, le=5)
    model_input_usd_per_million_tokens: Decimal = Field(default=Decimal("3"), ge=0)
    model_output_usd_per_million_tokens: Decimal = Field(default=Decimal("15"), ge=0)

    @field_validator("service_token")
    @classmethod
    def _service_token_long_enough(cls, value: SecretStr) -> SecretStr:
        if len(value.get_secret_value()) < MIN_SERVICE_TOKEN_LENGTH:
            raise ValueError("service_token must be at least 32 characters")
        return value

    @model_validator(mode="after")
    def _anthropic_needs_key(self) -> Self:
        key = self.anthropic_api_key
        if self.llm_provider == "anthropic" and (key is None or not key.get_secret_value().strip()):
            raise ValueError("anthropic_api_key is required when llm_provider is anthropic")
        return self


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()
