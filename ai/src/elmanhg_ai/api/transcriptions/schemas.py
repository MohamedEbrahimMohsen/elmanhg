from enum import StrEnum

from pydantic import Base64Bytes, Field

from elmanhg_ai.core.models import ApiInModel, ApiOutModel


class AudioContentType(StrEnum):
    WEBM = "audio/webm"
    OGG = "audio/ogg"
    MP4 = "audio/mp4"


class TranscriptionIn(ApiInModel):
    audio: Base64Bytes
    content_type: AudioContentType
    language: str = Field(default="ar", pattern=r"^[a-z]{2}$")
    duration_seconds: int = Field(ge=1, le=3600)


class TranscriptionOut(ApiOutModel):
    text: str
    model: str
    language: str
