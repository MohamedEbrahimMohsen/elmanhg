import multiprocessing
import multiprocessing.pool
import os
import sys
from typing import Final

from elmanhg_ai.cas.check import evaluate
from elmanhg_ai.cas.models import CasRequest, CheckOutcome
from elmanhg_ai.settings import Settings

BYTES_PER_MEGABYTE: Final = 1024 * 1024


def limit_worker_memory(megabytes: int) -> None:
    if sys.platform != "win32":
        import resource

        limit = megabytes * BYTES_PER_MEGABYTE
        resource.setrlimit(resource.RLIMIT_AS, (limit, limit))


def worker_id() -> int:
    return os.getpid()


class CasWorker:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._pool: multiprocessing.pool.Pool | None = None

    @property
    def started(self) -> bool:
        return self._pool is not None

    def submit(self, request: CasRequest) -> "multiprocessing.pool.AsyncResult[CheckOutcome]":
        return self._current().apply_async(evaluate, (request,))

    def warm(self) -> int:
        return int(self._current().apply(worker_id))

    def discard(self) -> None:
        pool, self._pool = self._pool, None
        if pool is not None:
            pool.terminate()
            pool.join()

    def _current(self) -> multiprocessing.pool.Pool:
        if self._pool is None:
            self._pool = self._create()
        return self._pool

    def _create(self) -> multiprocessing.pool.Pool:
        methods = multiprocessing.get_all_start_methods()
        method = "forkserver" if "forkserver" in methods else "spawn"
        context = multiprocessing.get_context(method)
        if method == "forkserver":
            context.set_forkserver_preload(["elmanhg_ai.cas.worker"])
        return context.Pool(
            processes=1,
            initializer=limit_worker_memory,
            initargs=(self._settings.cas_worker_memory_mb,),
            maxtasksperchild=self._settings.cas_max_tasks_per_worker,
        )
