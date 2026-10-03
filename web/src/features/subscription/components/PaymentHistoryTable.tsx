import { useTranslation } from 'react-i18next';
import type { PaymentResult, PaymentStatus } from '@/shared/api/generated/model';
import { formatDate, formatMoney } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

export interface PaymentHistoryTableProps {
  items: PaymentResult[];
}

const headerKeys = ['date', 'plan', 'amount', 'status'] as const;

const cellClassName = 'px-2.5 py-2.25 text-caption';

const statusClasses: Record<PaymentStatus, string> = {
  Succeeded: 'bg-success-soft text-success-text',
  Failed: 'bg-danger-soft text-danger',
  Refunded: 'bg-soft text-text-muted',
  Pending: 'bg-soft text-text-muted',
};

export function PaymentHistoryTable({ items }: PaymentHistoryTableProps) {
  const { t, i18n } = useTranslation('subscription');
  const lng = i18n.language;

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('payments.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th
                key={key}
                scope="col"
                className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
              >
                {t(`payments.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-border">
              <td className={cn(cellClassName, 'whitespace-nowrap')}>
                {formatDate(new Date(item.completedAt ?? item.createdAt), lng, 'arabic-indic', { dateStyle: 'medium' })}
              </td>
              <td className={cellClassName}>{t(`plan.${item.plan}`)}</td>
              <td className={cn(cellClassName, 'whitespace-nowrap')}>
                {formatMoney(Number(item.amount.amountMinor), item.amount.currency, lng)}
              </td>
              <td className={cellClassName}>
                <span className={cn('rounded-pill px-2.5 py-0.5 text-micro font-semibold', statusClasses[item.status])}>
                  {t(`payments.${item.status}`)}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
