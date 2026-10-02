import { useTranslation } from 'react-i18next';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import { formatDate, formatMoney } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { PaymentStatusBadge } from './PaymentStatusBadge';
import { refundsOffNoticeId } from './RefundsOffNotice';

export interface PaymentLogRowProps {
  item: AdminPaymentResult;
  refundsOff: boolean;
  onRefund: (item: AdminPaymentResult) => void;
  onKeep: (item: AdminPaymentResult) => void;
  onStudent: (studentId: string) => void;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';
const captionClassName = 'block text-caption text-text-muted';

export function PaymentLogRow({ item, refundsOff, onRefund, onKeep, onStudent }: PaymentLogRowProps) {
  const { t, i18n } = useTranslation('payments');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = (value: string) =>
    formatDate(new Date(value), lng, 'latin', { dateStyle: 'medium', timeStyle: 'short' });

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'whitespace-nowrap')}>{date(item.createdAt)}</td>
      <td className={cellClassName}>
        <button
          type="button"
          aria-label={t('table.showStudent', { name: item.studentName })}
          onClick={() => {
            onStudent(item.studentId);
          }}
          className="text-start font-semibold text-accent hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden"
        >
          {item.studentName}
        </button>
        {item.studentContact ? (
          <span dir="ltr" className={captionClassName}>
            {item.studentContact}
          </span>
        ) : null}
      </td>
      <td className={cellClassName}>
        {t(`plan.${item.plan}`)}
        <span className={captionClassName}>{t(`period.${item.period}`)}</span>
      </td>
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        {formatMoney(Number(item.amount.amountMinor), item.amount.currency, lng, 'latin')}
      </td>
      <td className={cellClassName}>
        <div className="flex flex-col items-start gap-1.5">
          <PaymentStatusBadge status={item.status} />
          {item.needsReview && item.reviewReason ? (
            <>
              <span className="inline-flex rounded-pill bg-danger-soft px-2.5 py-0.5 text-micro font-semibold text-danger">
                {t('review.badge')}
              </span>
              <span className={captionClassName}>{t(`review.${item.reviewReason}`)}</span>
            </>
          ) : null}
          {item.status === 'Refunded' && item.refundedAt ? (
            <span className={captionClassName}>
              {t('table.refundedLine', { date: date(item.refundedAt), reason: item.refundReason ?? '' })}
            </span>
          ) : null}
        </div>
      </td>
      <td className={cellClassName}>
        <span dir="ltr" className="font-mono text-mono break-all">
          {item.paymobTransactionId}
        </span>
      </td>
      <td className={cellClassName}>
        <div className="flex flex-wrap gap-2">
          {item.canRefund ? (
            <Button
              variant="danger"
              size="sm"
              disabled={refundsOff}
              aria-describedby={refundsOff ? refundsOffNoticeId : undefined}
              onClick={() => {
                onRefund(item);
              }}
            >
              {t('actions.refund')}
            </Button>
          ) : null}
          {item.needsReview ? (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                onKeep(item);
              }}
            >
              {t('actions.keep')}
            </Button>
          ) : null}
        </div>
      </td>
    </tr>
  );
}
