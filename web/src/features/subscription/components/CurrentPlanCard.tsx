import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { EntitlementResult } from '@/shared/api/generated/model';
import { planLabelKey } from '../api/entitlement';
import { SubscriptionStatusLine } from './SubscriptionStatusLine';

export interface CurrentPlanCardProps {
  entitlement: EntitlementResult;
}

export function CurrentPlanCard({ entitlement }: CurrentPlanCardProps) {
  const { t } = useTranslation('subscription');
  const headingId = useId();

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h3 id={headingId} className="text-caption font-semibold text-text-muted">
        {t('current.label')}
      </h3>
      <p className="font-semibold">{t(`current.${planLabelKey(entitlement)}`)}</p>
      <ul className="flex flex-col gap-1">
        {entitlement.subscriptions.map((subscription) => (
          <SubscriptionStatusLine key={subscription.id} subscription={subscription} />
        ))}
      </ul>
    </section>
  );
}
