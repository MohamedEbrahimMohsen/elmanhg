from decimal import Decimal

from elmanhg_ai.clients.model import estimate_cost_usd
from elmanhg_ai.settings import Settings


def test_estimate_cost_usd_uses_configured_prices(settings: Settings) -> None:
    cost = estimate_cost_usd(1_000, 2_000, settings)

    assert cost == Decimal("0.033000")
