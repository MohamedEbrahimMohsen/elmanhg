import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import type { ExamPeriodMutations } from '../hooks/useExamPeriodMutations';
import { examPeriodSchema, type ExamPeriodValues } from '../schemas/examPeriodSchema';

export interface ExamPeriodDialogProps {
  open: boolean;
  examPeriod: ExamPeriodResult | null;
  onOpenChange: (open: boolean) => void;
  mutations: ExamPeriodMutations;
}

const serverErrorFields = {
  EXAM_PERIOD_NAME_REQUIRED: 'name',
  EXAM_PERIOD_NAME_TOO_LONG: 'name',
  EXAM_PERIOD_START_DATE_REQUIRED: 'startDate',
  EXAM_PERIOD_END_DATE_REQUIRED: 'endDate',
  EXAM_PERIOD_DATE_RANGE_INVALID: 'endDate',
  EXAM_PERIOD_TOO_LONG: 'endDate',
} as const;

export function ExamPeriodDialog({ open, examPeriod, onOpenChange, mutations }: ExamPeriodDialogProps) {
  const { t } = useTranslation('configuration');
  const form = useForm<ExamPeriodValues>({
    resolver: zodResolver(examPeriodSchema),
    defaultValues: {
      name: examPeriod?.name ?? '',
      startDate: examPeriod?.startDate ?? '',
      endDate: examPeriod?.endDate ?? '',
    },
  });

  const submit = async (values: ExamPeriodValues) => {
    await (examPeriod ? mutations.update(examPeriod.id, values) : mutations.create(values));
    onOpenChange(false);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent title={t(examPeriod ? 'examPeriods.form.editTitle' : 'examPeriods.form.createTitle')}>
        <Form form={form} onSubmit={submit} serverErrorFields={serverErrorFields}>
          <TextField<ExamPeriodValues> name="name" label={t('examPeriods.form.name')} />
          <TextField<ExamPeriodValues> name="startDate" type="date" label={t('examPeriods.form.startDate')} />
          <TextField<ExamPeriodValues> name="endDate" type="date" label={t('examPeriods.form.endDate')} />
          <FormRootError />
          <div className="flex flex-wrap justify-end gap-3">
            <Button
              variant="secondary"
              onClick={() => {
                onOpenChange(false);
              }}
            >
              {t('examPeriods.form.cancel')}
            </Button>
            <Button type="submit" disabled={mutations.isPending} aria-busy={mutations.isPending}>
              {t('examPeriods.form.save')}
            </Button>
          </div>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
