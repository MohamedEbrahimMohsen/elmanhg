import json
from pathlib import Path

import pytest

from elmanhg_ai.openapi_export import build_openapi_document

pytestmark = pytest.mark.integration

COMMITTED_DOCUMENT = Path(__file__).parents[2] / "openapi" / "v1.json"


def test_openapi_document_matches_committed_file() -> None:
    committed = json.loads(COMMITTED_DOCUMENT.read_text(encoding="utf-8"))

    document = build_openapi_document()

    assert committed == document
    assert "/v1/chat" in document["paths"]
    assert not any(path.startswith("/health") for path in document["paths"])
