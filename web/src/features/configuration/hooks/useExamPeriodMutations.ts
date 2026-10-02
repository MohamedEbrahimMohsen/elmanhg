import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetExamPeriodsQueryKey,
  useCreateExamPeriod,
  useDeleteExamPeriod,
  useUpdateExamPeriod,
} from '@/shared/api/generated/configuration/configuration';
import type { ExamPeriodResult } from '@/shared/api/generated/model';
import type { ExamPeriodValues } from '../schemas/examPeriodSchema';

export interface ExamPeriodMutations {
  create: (values: ExamPeriodValues) => Promise<ExamPeriodResult>;
  update: (id: string, values: ExamPeriodValues) => Promise<ExamPeriodResult>;
  remove: (id: string) => Promise<void>;
  isPending: boolean;
}

export function useExamPeriodMutations(): ExamPeriodMutations {
  const { t } = useTranslation('configuration');
  const queryClient = useQueryClient();
  const onSuccess = (message: string) => () => {
    void queryClient.invalidateQueries({ queryKey: getGetExamPeriodsQueryKey() });
    toast.success(t(message));
  };
  const onError = () => {
    toast.error(t('examPeriods.toast.failed'));
  };
  const create = useCreateExamPeriod({ mutation: { onSuccess: onSuccess('examPeriods.toast.created'), onError } });
  const update = useUpdateExamPeriod({ mutation: { onSuccess: onSuccess('examPeriods.toast.updated'), onError } });
  const remove = useDeleteExamPeriod({ mutation: { onSuccess: onSuccess('examPeriods.toast.deleted'), onError } });

  return {
    create: (values) => create.mutateAsync({ data: values }),
    update: (id, values) => update.mutateAsync({ examPeriodId: id, data: values }),
    remove: (id) => remove.mutateAsync({ examPeriodId: id }),
    isPending: create.isPending || update.isPending || remove.isPending,
  };
}
