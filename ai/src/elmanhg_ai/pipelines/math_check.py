import time
from dataclasses import dataclass
from typing import Final

import structlog

from elmanhg_ai.api.math_checks.schemas import MathCheckIn
from elmanhg_ai.cas.models import CasLimits, CasRequest, Tolerance, Verdict
from elmanhg_ai.cas.pool import CasChecker
from elmanhg_ai.core.errors import FieldError, ValidationFailedError
from elmanhg_ai.settings import Settings

PIPELINE_NAME: Final = "math_check"

logger: Final = structlog.stdlib.get_logger(__name__)


@dataclass(frozen=True, slots=True)
class MathCheckResult:
    verdict: Verdict
    matched_index: int | None
    invalid_expected: tuple[int, ...]


def _limit_errors(payload: MathCheckIn, settings: Settings) -> list[FieldError]:
    errors: list[FieldError] = []
    if len(payload.answer) > settings.cas_max_answer_chars:
        limit = settings.cas_max_answer_chars
        errors.append(FieldError("answer", "TOO_LONG", f"at most {limit} characters"))
    if len(payload.expected) > settings.cas_max_expected:
        limit = settings.cas_max_expected
        errors.append(FieldError("expected", "TOO_MANY_ITEMS", f"at most {limit} answers"))
    limit = settings.cas_max_expected_chars
    errors.extend(
        FieldError(f"expected[{index}]", "TOO_LONG", f"at most {limit} characters")
        for index, text in enumerate(payload.expected)
        if len(text) > limit
    )
    return errors


async def run(payload: MathCheckIn, *, checker: CasChecker, settings: Settings) -> MathCheckResult:
    errors = _limit_errors(payload, settings)
    if errors:
        raise ValidationFailedError(errors)
    tolerance = None
    if payload.tolerance is not None and payload.tolerance_mode is not None:
        tolerance = Tolerance(payload.tolerance, payload.tolerance_mode)
    request = CasRequest(
        answer=payload.answer,
        expected=tuple(payload.expected),
        form=payload.form,
        tolerance=tolerance,
        limits=CasLimits.from_settings(settings),
    )
    started = time.perf_counter()
    outcome = await checker.check(request)
    latency_ms = round((time.perf_counter() - started) * 1000)
    logger.info(
        "math_check.completed",
        pipeline=PIPELINE_NAME,
        verdict=outcome.verdict.value,
        form=payload.form.value,
        expected_count=len(payload.expected),
        invalid_expected=len(outcome.invalid_expected),
        answer_chars=len(payload.answer),
        latency_ms=latency_ms,
    )
    return MathCheckResult(outcome.verdict, outcome.matched_index, outcome.invalid_expected)
