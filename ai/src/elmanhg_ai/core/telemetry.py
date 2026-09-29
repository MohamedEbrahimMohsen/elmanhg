from dataclasses import dataclass
from typing import Final

from fastapi import FastAPI
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import MetricReader, PeriodicExportingMetricReader
from opentelemetry.sdk.resources import SERVICE_NAME, SERVICE_VERSION, Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.sdk.trace.sampling import ParentBased, TraceIdRatioBased
from pydantic import SecretStr

from elmanhg_ai.settings import Settings

HEALTH_URLS: Final = "health"
DEPLOYMENT_ENVIRONMENT: Final = "deployment.environment.name"
MILLISECONDS_PER_SECOND: Final = 1000


@dataclass(frozen=True, slots=True)
class Telemetry:
    tracer_provider: TracerProvider
    meter_provider: MeterProvider
    exporting: bool

    def shutdown(self) -> None:
        self.tracer_provider.shutdown()
        self.meter_provider.shutdown()


def parse_otlp_headers(value: SecretStr | None) -> dict[str, str]:
    if value is None:
        return {}
    pairs = (item.partition("=") for item in value.get_secret_value().split(","))
    return {key.strip(): header.strip() for key, _, header in pairs}


def build_telemetry(settings: Settings) -> Telemetry:
    resource = Resource.create(
        {
            SERVICE_NAME: settings.otel_service_name,
            SERVICE_VERSION: settings.service_version,
            DEPLOYMENT_ENVIRONMENT: settings.env,
        }
    )
    tracer_provider = TracerProvider(
        resource=resource, sampler=ParentBased(TraceIdRatioBased(settings.trace_sample_ratio))
    )
    readers: list[MetricReader] = []
    if settings.otlp_endpoint:
        headers = parse_otlp_headers(settings.otlp_headers)
        tracer_provider.add_span_processor(
            BatchSpanProcessor(OTLPSpanExporter(endpoint=settings.otlp_endpoint, headers=headers))
        )
        readers.append(
            PeriodicExportingMetricReader(
                OTLPMetricExporter(endpoint=settings.otlp_endpoint, headers=headers),
                export_interval_millis=settings.metric_export_interval_seconds
                * MILLISECONDS_PER_SECOND,
            )
        )
    meter_provider = MeterProvider(resource=resource, metric_readers=readers)
    return Telemetry(
        tracer_provider=tracer_provider,
        meter_provider=meter_provider,
        exporting=settings.otlp_endpoint is not None,
    )


def instrument_app(app: FastAPI, telemetry: Telemetry) -> None:
    FastAPIInstrumentor.instrument_app(
        app,
        tracer_provider=telemetry.tracer_provider,
        meter_provider=telemetry.meter_provider,
        excluded_urls=HEALTH_URLS,
    )
