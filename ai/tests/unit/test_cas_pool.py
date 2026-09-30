import asyncio
import dataclasses
from typing import Final

import pytest
from structlog.testing import LogCapture

from elmanhg_ai.cas.models import AnswerForm, CasLimits, CasRequest, Verdict
from elmanhg_ai.cas.pool import CasPool
from elmanhg_ai.cas.worker import CasWorker
from elmanhg_ai.settings import Settings

# A spawned worker imports SymPy first; this leaves room for a slow CI runner.
COLD_START_TIMEOUT_SECONDS: Final = 60.0
HOSTILE_TIMEOUT_SECONDS: Final = 2.0
TOWER: Final = "((9^{999})^{999})^{999}"


def request(settings: Settings) -> CasRequest:
    return CasRequest(
        "x+x", ("2x",), AnswerForm.EQUIVALENT, None, CasLimits.from_settings(settings)
    )


def hostile(settings: Settings) -> CasRequest:
    unbounded = dataclasses.replace(CasLimits.from_settings(settings), max_magnitude=10**12)
    return CasRequest(TOWER, ("1",), AnswerForm.EQUIVALENT, None, unbounded)


async def test_cas_pool_check_returns_worker_outcome(settings: Settings) -> None:
    pool_settings = settings.model_copy(
        update={"cas_workers": 1, "cas_timeout_seconds": COLD_START_TIMEOUT_SECONDS}
    )
    pool = CasPool(pool_settings)

    try:
        outcome = await pool.check(request(pool_settings))
    finally:
        pool.close()

    assert (outcome.verdict, outcome.matched_index) == (Verdict.EQUIVALENT, 0)


async def test_cas_pool_timeout_returns_unchecked_and_discards_pool(settings: Settings) -> None:
    pool_settings = settings.model_copy(update={"cas_workers": 1, "cas_timeout_seconds": 0.001})
    pool = CasPool(pool_settings)

    try:
        before = await pool.warm()
        outcome = await pool.check(hostile(pool_settings))
        after = await pool.warm()
    finally:
        await pool.aclose()

    assert outcome.verdict == Verdict.UNCHECKED
    assert after != before
    assert pool.started is False


async def test_cas_pool_close_is_idempotent(settings: Settings) -> None:
    pool_settings = settings.model_copy(
        update={"cas_workers": 1, "cas_timeout_seconds": COLD_START_TIMEOUT_SECONDS}
    )
    pool = CasPool(pool_settings)
    await pool.check(request(pool_settings))
    started = pool.started

    pool.close()
    pool.close()

    assert started is True
    assert pool.started is False


async def test_cas_pool_hostile_timeout_spares_concurrent_benign_check(settings: Settings) -> None:
    pool_settings = settings.model_copy(
        update={"cas_workers": 2, "cas_timeout_seconds": HOSTILE_TIMEOUT_SECONDS}
    )
    pool = CasPool(pool_settings)

    try:
        before = await pool.warm()
        slow = asyncio.create_task(pool.check(hostile(pool_settings)))
        await asyncio.sleep(HOSTILE_TIMEOUT_SECONDS / 2)
        benign = await pool.check(request(pool_settings))
        timed_out = await slow
        after = await pool.warm()
    finally:
        await pool.aclose()

    assert (benign.verdict, benign.matched_index) == (Verdict.EQUIVALENT, 0)
    assert timed_out.verdict == Verdict.UNCHECKED
    assert len(set(before) & set(after)) == 1


async def test_cas_pool_queue_wait_is_not_charged_to_timeout(settings: Settings) -> None:
    pool_settings = settings.model_copy(
        update={"cas_workers": 1, "cas_timeout_seconds": HOSTILE_TIMEOUT_SECONDS}
    )
    pool = CasPool(pool_settings)

    try:
        await pool.warm()
        slow = asyncio.create_task(pool.check(hostile(pool_settings)))
        await asyncio.sleep(0)
        benign = await pool.check(request(pool_settings))
        timed_out = await slow
    finally:
        await pool.aclose()

    assert (benign.verdict, benign.matched_index) == (Verdict.EQUIVALENT, 0)
    assert timed_out.verdict == Verdict.UNCHECKED


class _FailingResult:
    def get(self, timeout: float) -> None:
        raise SystemError("worker state corrupted")


async def test_cas_pool_unknown_worker_exception_returns_unchecked_and_recycles(
    settings: Settings, monkeypatch: pytest.MonkeyPatch, log_capture: LogCapture
) -> None:
    warmed: list[CasWorker] = []

    def warm(worker: CasWorker) -> int:
        warmed.append(worker)
        return 1

    monkeypatch.setattr(CasWorker, "submit", lambda _worker, _request: _FailingResult())
    monkeypatch.setattr(CasWorker, "warm", warm)
    pool_settings = settings.model_copy(update={"cas_workers": 1})
    pool = CasPool(pool_settings)

    try:
        outcome = await pool.check(request(pool_settings))
    finally:
        await pool.aclose()

    assert outcome.verdict == Verdict.UNCHECKED
    assert len(warmed) == 1
    assert pool.started is False
    [entry] = [e for e in log_capture.entries if e["event"] == "math_check.worker_failed"]
    assert entry["error_type"] == "SystemError"
