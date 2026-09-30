import asyncio
import multiprocessing
from typing import Final, Protocol

import structlog

from elmanhg_ai.cas.models import CasRequest, CheckOutcome, Verdict
from elmanhg_ai.cas.worker import CasWorker
from elmanhg_ai.settings import Settings

WORKER_FAILURES: Final = (
    MemoryError,
    RecursionError,
    ArithmeticError,
    ValueError,
    TypeError,
    NotImplementedError,
)
UNCHECKED: Final = CheckOutcome(Verdict.UNCHECKED, None, ())

logger: Final = structlog.stdlib.get_logger(__name__)


class CasChecker(Protocol):
    async def check(self, request: CasRequest) -> CheckOutcome: ...


# Each worker is its own single-process pool and is checked out for one request at a time,
# so queue wait is never charged to the timeout and a timeout kills only the offending process.
class CasPool:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._workers = [CasWorker(settings) for _ in range(settings.cas_workers)]
        self._idle: asyncio.Queue[CasWorker] = asyncio.Queue()
        self._recycling: set[asyncio.Task[None]] = set()
        for worker in self._workers:
            self._idle.put_nowait(worker)

    @property
    def started(self) -> bool:
        return any(worker.started for worker in self._workers)

    async def check(self, request: CasRequest) -> CheckOutcome:
        worker = await self._idle.get()
        try:
            pending = await asyncio.to_thread(worker.submit, request)
            outcome = await asyncio.to_thread(pending.get, self._settings.cas_timeout_seconds)
        except multiprocessing.TimeoutError:
            logger.warning("math_check.timeout", timeout_seconds=self._settings.cas_timeout_seconds)
            self._recycle(worker)
            return UNCHECKED
        except WORKER_FAILURES as error:
            logger.warning("math_check.worker_failed", error_type=type(error).__name__)
            self._idle.put_nowait(worker)
            return UNCHECKED
        except BaseException:
            self._recycle(worker)
            raise
        self._idle.put_nowait(worker)
        return outcome

    async def warm(self) -> list[int]:
        workers = [await self._idle.get() for _ in self._workers]
        try:
            return [await asyncio.to_thread(worker.warm) for worker in workers]
        finally:
            for worker in workers:
                self._idle.put_nowait(worker)

    async def aclose(self) -> None:
        await asyncio.gather(*self._recycling, return_exceptions=True)
        await asyncio.to_thread(self.close)

    def close(self) -> None:
        for worker in self._workers:
            worker.discard()

    def _recycle(self, worker: CasWorker) -> None:
        task = asyncio.create_task(self._replace(worker))
        self._recycling.add(task)
        task.add_done_callback(self._recycling.discard)

    async def _replace(self, worker: CasWorker) -> None:
        try:
            await asyncio.to_thread(worker.discard)
            await asyncio.to_thread(worker.warm)
        finally:
            self._idle.put_nowait(worker)
