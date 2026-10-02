from elmanhg_ai.core.models import ApiOutModel


class SecretStatusOut(ApiOutModel):
    key: str
    is_set: bool


class ConfigurationOut(ApiOutModel):
    llm_provider: str
    chat_model: str
    essay_grading_model: str
    math_step_grading_model: str
    embedding_provider: str
    embedding_model: str
    transcription_provider: str
    transcription_model: str
    secrets: list[SecretStatusOut]
