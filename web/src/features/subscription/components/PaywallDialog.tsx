import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetMyUsage } from '@/shared/api/generated/subscriptions/subscriptions';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import type { PaywallReason } from '../api/paywall';

export interface PaywallDialogProps {
  reason: PaywallReason | null;
  onClose: () => void;
}

export function PaywallDialog({ reason, onClose }: PaywallDialogProps) {
  const { t } = useTranslation('subscription');
  const { data: usage } = useGetMyUsage({ query: { enabled: reason === 'dailyQuiz' } });
  const shown = reason ?? 'dailyQuiz';
  const limit = usage?.dailyQuizQuestionLimit;
  const body =
    shown !== 'dailyQuiz'
      ? t(`paywall.${shown}.body`)
      : limit == null
        ? t('paywall.dailyQuiz.bodyNoLimit')
        : t('paywall.dailyQuiz.body', { count: Number(limit) });

  return (
    <Dialog
      open={reason !== null}
      onOpenChange={(open) => {
        if (!open) {
          onClose();
        }
      }}
    >
      <DialogContent title={t(`paywall.${shown}.title`)}>
        <p className="text-ui text-text">{body}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="secondary" onClick={onClose}>
            {t('paywall.later')}
          </Button>
          <Button asChild variant="primary">
            <Link to="/student/subscription">{t('paywall.subscribe')}</Link>
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
