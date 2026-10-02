import { useTranslation } from 'react-i18next';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import type { ExamPeriodMutations } from '../hooks/useExamPeriodMutations';

export interface DeleteExamPeriodDialogProps {
  examPeriod: ExamPeriodResult | null;
  onOpenChange: (open: boolean) => void;
  mutations: ExamPeriodMutations;
}

export function DeleteExamPeriodDialog({ examPeriod, onOpenChange, mutations }: DeleteExamPeriodDialogProps) {
  const { t } = useTranslation('configuration');

  const confirm = async () => {
    if (!examPeriod) {
      return;
    }
    await mutations.remove(examPeriod.id);
    onOpenChange(false);
  };

  return (
    <Dialog open={examPeriod !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('examPeriods.deleteDialog.title')}>
        <p className="text-ui text-text">{t('examPeriods.deleteDialog.body', { name: examPeriod?.name ?? '' })}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="secondary"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('examPeriods.deleteDialog.cancel')}
          </Button>
          <Button
            variant="danger"
            disabled={mutations.isPending}
            aria-busy={mutations.isPending}
            onClick={() => void confirm().catch(() => undefined)}
          >
            {t('examPeriods.deleteDialog.confirm')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
