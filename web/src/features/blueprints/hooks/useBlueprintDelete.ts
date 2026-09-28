import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetSubjectExamBlueprintsQueryKey,
  useDeleteExamBlueprint,
} from '@/shared/api/generated/exam-blueprints/exam-blueprints';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export function useBlueprintDelete(subjectId: string): (examBlueprintId: string) => void {
  const { t } = useTranslation('blueprints');
  const queryClient = useQueryClient();
  const deleteBlueprint = useDeleteExamBlueprint({
    mutation: {
      onSuccess: async () => {
        toast(t('toasts.deleted'));
        await queryClient.invalidateQueries({ queryKey: getGetSubjectExamBlueprintsQueryKey(subjectId) });
      },
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return (examBlueprintId) => {
    deleteBlueprint.mutate({ examBlueprintId });
  };
}
