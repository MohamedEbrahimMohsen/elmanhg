import { useTranslation } from 'react-i18next';
import type { PaymentStatus } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';

export interface PaymentStatusBadgeProps {
  status: PaymentStatus;
}

const statusClasses: Record<PaymentStatus, string> = {
  Succeeded: 'bg-success-soft text-success-text',
  Failed: 'bg-danger-soft text-danger',
  Pending: 'bg-soft text-text-muted',
  Refunded: 'bg-soft text-text-muted',
};

export function PaymentStatusBadge({ status }: PaymentStatusBadgeProps) {
  const { t } = useTranslation('payments');

  return (
    <span className={cn('inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold', statusClasses[status])}>
      {t(`status.${status}`)}
    </span>
  );
}
