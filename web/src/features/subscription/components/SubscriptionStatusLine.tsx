import { useTranslation } from 'react-i18next';
import type { SubscriptionResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';

export interface SubscriptionStatusLineProps {
  subscription: SubscriptionResult;
}

export function SubscriptionStatusLine({ subscription }: SubscriptionStatusLineProps) {
  const { t, i18n } = useTranslation('subscription');
  const { status, plan, currentPeriodEnd, entitledUntil } = subscription;

  if (status === 'Expired') {
    return null;
  }

  const date = formatDate(
    new Date(status === 'Active' ? currentPeriodEnd : entitledUntil),
    i18n.language,
    'arabic-indic',
    {
      dateStyle: 'medium',
    },
  );

  return <li className="text-caption text-text-muted">{t(`status.${status}`, { plan: t(`plan.${plan}`), date })}</li>;
}
