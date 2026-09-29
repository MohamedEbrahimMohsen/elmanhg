import { CreditCard } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export type PaymentLogEmptyVariant = 'no-data' | 'no-review' | 'no-results';

export interface PaymentLogEmptyStateProps {
  variant: PaymentLogEmptyVariant;
  onClear: () => void;
}

const messageKeys: Record<PaymentLogEmptyVariant, string> = {
  'no-data': 'empty.noData',
  'no-review': 'empty.noReview',
  'no-results': 'empty.noResults',
};

export function PaymentLogEmptyState({ variant, onClear }: PaymentLogEmptyStateProps) {
  const { t } = useTranslation('payments');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <CreditCard aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{t(messageKeys[variant])}</p>
      {variant === 'no-results' ? (
        <Button variant="primary" onClick={onClear}>
          {t('filters.clear')}
        </Button>
      ) : null}
    </div>
  );
}
