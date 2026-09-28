import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetSubjectExamBlueprintsQueryKey,
  useSaveSubjectExamBlueprint,
  useSaveUnitExamBlueprint,
} from '@/shared/api/generated/exam-blueprints/exam-blueprints';
import type { ExamBlueprintInput } from '@/shared/api/generated/model';

export interface BlueprintSave {
  saveSubjectDefault: (input: ExamBlueprintInput) => Promise<void>;
  saveUnit: (unitId: string, input: ExamBlueprintInput) => Promise<void>;
}

export function useBlueprintSave(subjectId: string): BlueprintSave {
  const { t } = useTranslation('blueprints');
  const queryClient = useQueryClient();
  const mutation = {
    onSuccess: () => {
      toast(t('toasts.saved'));
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: getGetSubjectExamBlueprintsQueryKey(subjectId) }),
  };
  const saveSubject = useSaveSubjectExamBlueprint({ mutation });
  const saveUnitBlueprint = useSaveUnitExamBlueprint({ mutation });

  return {
    saveSubjectDefault: async (input) => {
      await saveSubject.mutateAsync({ subjectId, data: input });
    },
    saveUnit: async (unitId, input) => {
      await saveUnitBlueprint.mutateAsync({ unitId, data: input });
    },
  };
}
