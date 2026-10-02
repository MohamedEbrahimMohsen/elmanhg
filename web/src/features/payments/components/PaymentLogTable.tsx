import { useTranslation } from 'react-i18next';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import { useRefundsEnabled } from '../hooks/useRefundsEnabled';
import { PaymentLogRow } from './PaymentLogRow';

export interface PaymentLogTableProps {
  items: AdminPaymentResult[];
  onRefund: (item: AdminPaymentResult) => void;
  onKeep: (item: AdminPaymentResult) => void;
  onStudent: (studentId: string) => void;
}

const headerKeys = ['date', 'student', 'plan', 'amount', 'status', 'reference', 'actions'] as const;

export function PaymentLogTable({ items, onRefund, onKeep, onStudent }: PaymentLogTableProps) {
  const { t } = useTranslation('payments');
  const { data: refundsEnabled } = useRefundsEnabled();

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('table.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th
                key={key}
                scope="col"
                className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
              >
                {t(`table.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <PaymentLogRow
              key={item.id}
              item={item}
              refundsEnabled={refundsEnabled}
              onRefund={onRefund}
              onKeep={onKeep}
              onStudent={onStudent}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
