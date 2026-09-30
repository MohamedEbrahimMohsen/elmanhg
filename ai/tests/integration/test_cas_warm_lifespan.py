from typing import Final

import pytest

from elmanhg_ai.cas.pool import CasPool
from elmanhg_ai.clients.fake_model import FakeModelClient
from elmanhg_ai.main import create_app
from elmanhg_ai.settings import Settings

pytestmark = pytest.mark.integration

# A spawned worker imports SymPy first; this leaves room for a slow CI runner.
COLD_START_TIMEOUT_SECONDS: Final = 60.0


async def test_lifespan_warms_cas_pool_when_enabled(settings: Settings) -> None:
    warm_settings = settings.model_copy(
        update={
            "cas_warm_on_start": True,
            "cas_workers": 1,
            "cas_timeout_seconds": COLD_START_TIMEOUT_SECONDS,
        }
    )
    app = create_app(warm_settings, model_client=FakeModelClient())

    async with app.router.lifespan_context(app):
        pool: CasPool = app.state.cas_pool
        started = pool.started

    assert started is True


async def test_lifespan_skips_warm_when_disabled(settings: Settings) -> None:
    app = create_app(settings.model_copy(update={"cas_workers": 1}), model_client=FakeModelClient())

    async with app.router.lifespan_context(app):
        pool: CasPool = app.state.cas_pool
        started = pool.started

    assert started is False
