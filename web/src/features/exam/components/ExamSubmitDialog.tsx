import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';

export interface ExamSubmitDialogProps {
  open: boolean;
  unansweredCount: number;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}

export function ExamSubmitDialog({ open, unansweredCount, isPending, onOpenChange, onConfirm }: ExamSubmitDialogProps) {
  const { t } = useTranslation('exam');

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent title={t('exam.confirmTitle')}>
        {unansweredCount > 0 ? (
          <p className="text-ui text-text">{t('exam.confirmUnanswered', { count: unansweredCount })}</p>
        ) : null}
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="secondary"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('exam.cancel')}
          </Button>
          <Button variant="primary" disabled={isPending} onClick={onConfirm}>
            {t('exam.confirmSubmit')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
