import { useTranslation } from 'react-i18next';
import type { AdminPaymentResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';

export interface ResolveReviewDialogProps {
  payment: AdminPaymentResult | null;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}

export function ResolveReviewDialog({ payment, isPending, onOpenChange, onConfirm }: ResolveReviewDialogProps) {
  const { t } = useTranslation('payments');

  return (
    <Dialog open={payment !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('resolve.title')}>
        <p className="text-ui text-text">{t('resolve.body')}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="secondary"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('resolve.cancel')}
          </Button>
          <Button variant="primary" disabled={isPending} onClick={onConfirm}>
            {t('resolve.confirm')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
