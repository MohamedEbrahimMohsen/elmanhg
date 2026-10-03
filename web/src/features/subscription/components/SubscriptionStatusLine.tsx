import { useTranslation } from 'react-i18next';
import type { SubscriptionResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { Button } from '@/shared/ui/button';

export interface SubscriptionStatusLineProps {
  subscription: SubscriptionResult;
  onCancelClick: (subscription: SubscriptionResult) => void;
}

export function SubscriptionStatusLine({ subscription, onCancelClick }: SubscriptionStatusLineProps) {
  const { t, i18n } = useTranslation('subscription');
  const { status, plan, currentPeriodEnd, entitledUntil } = subscription;

  if (status === 'Expired') {
    return null;
  }

  const inGrace = status === 'Active' && subscription.inGracePeriod;
  const date = formatDateTime(inGrace || status !== 'Active' ? entitledUntil : currentPeriodEnd, i18n.language, 'date');
  const planLabel = t(`plan.${plan}`);

  return (
    <li className="flex flex-wrap items-center justify-between gap-2 text-caption text-text-muted">
      <span>{t(inGrace ? 'status.ActiveGrace' : `status.${status}`, { plan: planLabel, date })}</span>
      {status === 'Active' || status === 'PastDue' ? (
        <Button
          variant="danger"
          size="sm"
          aria-label={t('cancel.buttonLabel', { plan: planLabel })}
          onClick={() => {
            onCancelClick(subscription);
          }}
        >
          {t('cancel.button')}
        </Button>
      ) : null}
    </li>
  );
}
