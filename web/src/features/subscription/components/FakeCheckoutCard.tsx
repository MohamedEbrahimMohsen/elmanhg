import { useId } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { PaymentResult } from '@/shared/api/generated/model';
import { formatMoney } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';

export interface FakeCheckoutCardProps {
  payment: PaymentResult;
  isPending: boolean;
  onComplete: (succeeded: boolean) => void;
}

export function FakeCheckoutCard({ payment, isPending, onComplete }: FakeCheckoutCardProps) {
  const { t, i18n } = useTranslation('subscription');
  const headingId = useId();

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h1 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('fakeCheckout.title')}
      </h1>
      <p className="text-ui">
        {t('fakeCheckout.summary', {
          plan: t(`plan.${payment.plan}`),
          period: t(`period.${payment.period}`),
          amount: formatMoney(Number(payment.amount.amountMinor), payment.amount.currency, i18n.language),
        })}
      </p>
      <div className="rounded-md bg-soft p-3 text-ui">{t('fakeCheckout.card')}</div>
      <p className="text-caption text-text-muted">{t('fakeCheckout.note')}</p>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="accent"
          disabled={isPending}
          onClick={() => {
            onComplete(true);
          }}
        >
          {t('fakeCheckout.succeed')}
        </Button>
        <Button
          variant="danger"
          disabled={isPending}
          onClick={() => {
            onComplete(false);
          }}
        >
          {t('fakeCheckout.fail')}
        </Button>
        <Button variant="secondary" asChild>
          <Link to="/student/subscription">{t('fakeCheckout.cancel')}</Link>
        </Button>
      </div>
    </section>
  );
}
