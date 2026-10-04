import { useTranslation } from 'react-i18next';
import type { BillingPeriod, PlanPriceResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface BaseCheckoutActionsProps {
  mode: 'subscribe' | 'renew';
  prices: PlanPriceResult[];
  disabled: boolean;
  onCheckout: (period: BillingPeriod) => void;
}

export function BaseCheckoutActions({ mode, prices, disabled, onCheckout }: BaseCheckoutActionsProps) {
  const { t } = useTranslation('subscription');

  return prices.map((price) => (
    <Button
      key={price.period}
      variant="secondary"
      className="w-full"
      disabled={disabled}
      onClick={() => {
        onCheckout(price.period);
      }}
    >
      {t(`checkout.${mode}.${price.period}`)}
    </Button>
  ));
}
