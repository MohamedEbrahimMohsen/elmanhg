import json
from pathlib import Path
from typing import Any, Final

from pydantic import SecretStr

from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

# Settings requires a token; the exported document never serves a request.
EXPORT_SERVICE_TOKEN: Final = "openapi-export-placeholder-token-000000"  # noqa: S105
OUTPUT_PATH: Final = Path("openapi/v1.json")


def build_openapi_document() -> dict[str, Any]:
    settings = Settings(
        service_token=SecretStr(EXPORT_SERVICE_TOKEN), llm_provider="fake", env="testing"
    )
    return create_app(settings).openapi()


def main() -> None:
    document = json.dumps(build_openapi_document(), indent=2, ensure_ascii=False) + "\n"
    OUTPUT_PATH.write_text(document, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
