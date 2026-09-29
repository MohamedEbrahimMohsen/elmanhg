import { useTranslation } from 'react-i18next';
import type { BillingPeriod, PlanPriceResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface BaseCheckoutActionsProps {
  prices: PlanPriceResult[];
  disabled: boolean;
  onCheckout: (period: BillingPeriod) => void;
}

export function BaseCheckoutActions({ prices, disabled, onCheckout }: BaseCheckoutActionsProps) {
  const { t } = useTranslation('subscription');

  return prices.map((price) => (
    <Button
      key={price.period}
      variant="accent"
      className="w-full"
      disabled={disabled}
      onClick={() => {
        onCheckout(price.period);
      }}
    >
      {t(`checkout.subscribe.${price.period}`)}
    </Button>
  ));
}
