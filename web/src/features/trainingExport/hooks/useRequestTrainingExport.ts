import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetTrainingExportsQueryKey,
  useRequestTrainingExport as useRequestTrainingExportMutation,
} from '@/shared/api/generated/training-exports/training-exports';
import { toRequestTrainingExportBody } from '../api/trainingExportParams';
import type { TrainingExportRequestValues } from '../schemas/trainingExportRequestSchema';

export interface TrainingExportRequest {
  request: (values: TrainingExportRequestValues) => Promise<unknown>;
  isPending: boolean;
}

export function useRequestTrainingExport(): TrainingExportRequest {
  const { t } = useTranslation('trainingExport');
  const queryClient = useQueryClient();
  const mutation = useRequestTrainingExportMutation({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: [getGetTrainingExportsQueryKey()[0]] });
        toast.success(t('form.done'));
      },
    },
  });

  return {
    request: (values) => mutation.mutateAsync({ data: toRequestTrainingExportBody(values) }),
    isPending: mutation.isPending,
  };
}
