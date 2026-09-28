import { useQueryClient } from '@tanstack/react-query';
import { getGetSubjectQueryKey, getGetSubjectsQueryKey } from '@/shared/api/generated/subjects/subjects';
import { useCreateUnit, useDeleteUnit, useReorderUnit, useUpdateUnit } from '@/shared/api/generated/units/units';
import { useContentFeedback } from './useContentFeedback';

export interface UnitMutations {
  create: (name: string) => Promise<void>;
  rename: (unitId: string, name: string) => Promise<void>;
  move: (unitId: string, position: number) => void;
  remove: (unitId: string) => void;
}

export function useUnitMutations(subjectId: string): UnitMutations {
  const queryClient = useQueryClient();
  const { success, failure } = useContentFeedback();
  const settle = async (key: string) => {
    success(key);
    await queryClient.invalidateQueries({ queryKey: getGetSubjectQueryKey(subjectId) });
    await queryClient.invalidateQueries({ queryKey: getGetSubjectsQueryKey() });
  };
  const createUnit = useCreateUnit({ mutation: { onSuccess: () => settle('units.created') } });
  const updateUnit = useUpdateUnit({ mutation: { onSuccess: () => settle('units.renamed') } });
  const reorderUnit = useReorderUnit({ mutation: { onSuccess: () => settle('units.moved'), onError: failure } });
  const deleteUnit = useDeleteUnit({ mutation: { onSuccess: () => settle('units.deleted'), onError: failure } });

  return {
    create: async (name) => {
      await createUnit.mutateAsync({ subjectId, data: { name } });
    },
    rename: async (unitId, name) => {
      await updateUnit.mutateAsync({ subjectId, unitId, data: { name } });
    },
    move: (unitId, position) => {
      reorderUnit.mutate({ subjectId, unitId, data: { position } });
    },
    remove: (unitId) => {
      deleteUnit.mutate({ subjectId, unitId });
    },
  };
}
