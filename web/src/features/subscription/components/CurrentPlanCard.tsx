import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { EntitlementResult, SubscriptionResult } from '@/shared/api/generated/model';
import { planLabelKey } from '../api/entitlement';
import { CancelSubscriptionDialog } from './CancelSubscriptionDialog';
import { SubscriptionStatusLine } from './SubscriptionStatusLine';

export interface CurrentPlanCardProps {
  entitlement: EntitlementResult;
  onCancel: (subscriptionId: string, onDone: () => void) => void;
  isCancelPending: boolean;
}

export function CurrentPlanCard({ entitlement, onCancel, isCancelPending }: CurrentPlanCardProps) {
  const { t } = useTranslation('subscription');
  const headingId = useId();
  const [cancelling, setCancelling] = useState<SubscriptionResult | null>(null);

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h3 id={headingId} className="text-caption font-bold text-text-muted">
        {t('current.label')}
      </h3>
      <p className="font-bold">{t(`current.${planLabelKey(entitlement)}`)}</p>
      <ul className="flex flex-col gap-1">
        {entitlement.subscriptions.map((subscription) => (
          <SubscriptionStatusLine key={subscription.id} subscription={subscription} onCancelClick={setCancelling} />
        ))}
      </ul>
      <CancelSubscriptionDialog
        subscription={cancelling}
        isPending={isCancelPending}
        onOpenChange={(open) => {
          if (!open) setCancelling(null);
        }}
        onConfirm={() => {
          if (cancelling)
            onCancel(cancelling.id, () => {
              setCancelling(null);
            });
        }}
      />
    </section>
  );
}
