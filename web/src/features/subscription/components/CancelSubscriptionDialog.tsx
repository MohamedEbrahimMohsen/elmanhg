import { useTranslation } from 'react-i18next';
import type { SubscriptionResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';

export interface CancelSubscriptionDialogProps {
  subscription: SubscriptionResult | null;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}

export function CancelSubscriptionDialog({
  subscription,
  isPending,
  onOpenChange,
  onConfirm,
}: CancelSubscriptionDialogProps) {
  const { t, i18n } = useTranslation('subscription');
  const plan = subscription ? t(`plan.${subscription.plan}`) : '';
  const date = subscription ? formatDateTime(subscription.currentPeriodEnd, i18n.language, 'date') : '';

  return (
    <Dialog open={subscription !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('cancel.title', { plan })}>
        <p className="text-ui text-text">{t('cancel.body', { date })}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="secondary"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('cancel.keep')}
          </Button>
          <Button variant="danger" disabled={isPending} onClick={onConfirm}>
            {t('cancel.confirm')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
