import type { ReactNode } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { PaymentResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface CheckoutStatusCardProps {
  payment: PaymentResult;
  timedOut: boolean;
  isFetching: boolean;
  onCheckAgain: () => void;
}

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';
const titleClassName = 'font-display text-h2 font-bold lg:text-h2-desktop';

export function CheckoutStatusCard({ payment, timedOut, isFetching, onCheckAgain }: CheckoutStatusCardProps) {
  const { t } = useTranslation('subscription');
  const backLink = (variant: 'accent' | 'secondary' | 'ghost', label: string): ReactNode => (
    <Button variant={variant} asChild>
      <Link to="/student/subscription">{label}</Link>
    </Button>
  );

  if (payment.status === 'Succeeded') {
    return (
      <section className={cardClassName}>
        <h1 className={titleClassName}>{t('checkoutResult.succeededTitle')}</h1>
        <p className="text-ui">{t('checkoutResult.succeededBody', { plan: t(`plan.${payment.plan}`) })}</p>
        <div className="flex flex-wrap gap-2">{backLink('accent', t('checkoutResult.done'))}</div>
      </section>
    );
  }
  if (payment.status === 'Failed') {
    return (
      <section className={cardClassName}>
        <h1 className={titleClassName}>{t('checkoutResult.failedTitle')}</h1>
        <p className="text-ui">{t('checkoutResult.failedBody')}</p>
        <div className="flex flex-wrap gap-2">{backLink('secondary', t('checkoutResult.tryAgain'))}</div>
      </section>
    );
  }
  if (!timedOut) {
    return (
      <section className={cardClassName}>
        <div role="status" aria-live="polite" className="flex flex-col gap-3">
          <h1 className={titleClassName}>{t('checkoutResult.pendingTitle')}</h1>
          <p className="text-ui">{t('checkoutResult.pendingBody')}</p>
        </div>
      </section>
    );
  }
  return (
    <section className={cardClassName}>
      <h1 className={titleClassName}>{t('checkoutResult.slowTitle')}</h1>
      <p className="text-ui">{t('checkoutResult.slowBody')}</p>
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" disabled={isFetching} onClick={onCheckAgain}>
          {t('checkoutResult.checkAgain')}
        </Button>
        {backLink('ghost', t('checkoutResult.back'))}
      </div>
    </section>
  );
}
