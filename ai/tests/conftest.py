from collections.abc import AsyncIterator, Callable, Iterator
from pathlib import Path
from typing import Any, Final

import httpx2
import pytest
import structlog
from fastapi import FastAPI
from pydantic import SecretStr
from structlog.testing import LogCapture

from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

TEST_SERVICE_TOKEN: Final = "elmanhg-tests-ai-service-token-0123456789"
FIXTURES: Final = Path(__file__).parent / "fixtures"

SUBJECT_ID: Final = "0f5e2a4c-1b3d-4e6f-8a9b-0c1d2e3f4a5b"
UNIT_ID: Final = "1a2b3c4d-5e6f-4a1b-9c2d-3e4f5a6b7c8d"
LESSON_ID: Final = "2b3c4d5e-6f7a-4b2c-8d3e-4f5a6b7c8d9e"
QUESTION_ID: Final = "3c4d5e6f-7a8b-4c3d-9e4f-5a6b7c8d9e0f"


@pytest.fixture
def settings() -> Settings:
    return Settings(
        service_token=SecretStr(TEST_SERVICE_TOKEN),
        env="testing",
        llm_provider="fake",
        log_format="json",
    )


@pytest.fixture
def fake_model() -> FakeModelClient:
    return FakeModelClient()


@pytest.fixture
def app(settings: Settings, fake_model: FakeModelClient) -> FastAPI:
    return create_app(settings, model_client=fake_model)


@pytest.fixture
async def client(app: FastAPI) -> AsyncIterator[httpx2.AsyncClient]:
    async with (
        app.router.lifespan_context(app),
        httpx2.AsyncClient(transport=httpx2.ASGITransport(app=app), base_url="http://test") as c,
    ):
        yield c


@pytest.fixture
def auth_headers() -> dict[str, str]:
    return {"Authorization": f"Bearer {TEST_SERVICE_TOKEN}"}


@pytest.fixture
def chat_payload() -> Callable[..., dict[str, Any]]:
    def build(**overrides: Any) -> dict[str, Any]:
        payload: dict[str, Any] = {
            "context": {
                "entryPoint": "quizQuestion",
                "subject": {"id": SUBJECT_ID, "name": "الفيزياء"},
                "unit": {"id": UNIT_ID, "name": "الكهربية التيارية"},
                "lesson": {
                    "id": LESSON_ID,
                    "name": "قانون أوم",
                    "explanation": "شدة التيار تتناسب طرديا مع فرق الجهد.",
                    "objectives": ["يطبق الطالب قانون أوم"],
                    "summary": "المقاومة تساوي فرق الجهد مقسوما على شدة التيار.",
                },
                "question": {
                    "id": QUESTION_ID,
                    "stem": "احسب المقاومة إذا كان فرق الجهد ٨ فولت والتيار ٢ أمبير.",
                    "studentAnswer": "٢ أوم",
                    "correctAnswer": "٤ أوم",
                    "explanation": "المقاومة = ٨ ÷ ٢ = ٤ أوم.",
                },
            },
            "history": [
                {"role": "user", "content": "ما هو قانون أوم؟"},
                {"role": "assistant", "content": "فرق الجهد يساوي التيار في المقاومة."},
            ],
            "message": "لماذا إجابتي خطأ؟",
        }
        return payload | overrides

    return build


@pytest.fixture
def log_capture(app: FastAPI) -> Iterator[LogCapture]:
    capture = LogCapture()
    structlog.configure(processors=[structlog.contextvars.merge_contextvars, capture])
    yield capture
    structlog.reset_defaults()


@pytest.fixture
def anthropic_fixture() -> Callable[[str], str]:
    def read(name: str) -> str:
        return (FIXTURES / "anthropic" / name).read_text(encoding="utf-8")

    return read
