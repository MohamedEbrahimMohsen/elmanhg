from pydantic import SecretStr

from elmanhg_ai.core.telemetry import build_telemetry, parse_otlp_headers
from elmanhg_ai.settings import Settings

UNREACHABLE_COLLECTOR = "http://127.0.0.1:9"


def test_build_telemetry_without_endpoint_is_not_exporting(settings: Settings) -> None:
    telemetry = build_telemetry(settings)

    assert telemetry.exporting is False
    telemetry.shutdown()


def test_build_telemetry_with_endpoint_is_exporting(settings: Settings) -> None:
    telemetry = build_telemetry(
        settings.model_copy(update={"otlp_endpoint": UNREACHABLE_COLLECTOR})
    )

    assert telemetry.exporting is True
    telemetry.shutdown()


def test_build_telemetry_sets_service_resource(settings: Settings) -> None:
    telemetry = build_telemetry(settings.model_copy(update={"service_version": "1.2.3"}))

    attributes = telemetry.tracer_provider.resource.attributes
    assert attributes["service.name"] == "elmanhg-ai"
    assert attributes["service.version"] == "1.2.3"
    assert attributes["deployment.environment.name"] == "testing"
    telemetry.shutdown()


def test_parse_otlp_headers_returns_pairs() -> None:
    assert parse_otlp_headers(SecretStr("a=b, c = d=e")) == {"a": "b", "c": "d=e"}


def test_parse_otlp_headers_none_returns_empty() -> None:
    assert parse_otlp_headers(None) == {}
