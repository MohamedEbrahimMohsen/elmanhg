import { useQueryClient } from '@tanstack/react-query';
import {
  getGetSubjectQueryKey,
  getGetSubjectsQueryKey,
  useCreateSubject,
  useDeleteSubject,
  useReorderSubject,
  useUpdateSubject,
} from '@/shared/api/generated/subjects/subjects';
import { useContentFeedback } from './useContentFeedback';

export interface SubjectMutations {
  create: (name: string) => Promise<void>;
  rename: (subjectId: string, name: string) => Promise<void>;
  move: (subjectId: string, position: number) => void;
  remove: (subjectId: string) => void;
}

export function useSubjectMutations(): SubjectMutations {
  const queryClient = useQueryClient();
  const { success, failure } = useContentFeedback();
  const settle = async (key: string, subjectId?: string) => {
    success(key);
    await queryClient.invalidateQueries({ queryKey: getGetSubjectsQueryKey() });
    if (subjectId !== undefined) {
      await queryClient.invalidateQueries({ queryKey: getGetSubjectQueryKey(subjectId) });
    }
  };
  const createSubject = useCreateSubject({ mutation: { onSuccess: () => settle('subjects.created') } });
  const updateSubject = useUpdateSubject({
    mutation: { onSuccess: (_, { subjectId }) => settle('subjects.renamed', subjectId) },
  });
  const reorderSubject = useReorderSubject({
    mutation: { onSuccess: () => settle('subjects.moved'), onError: failure },
  });
  const deleteSubject = useDeleteSubject({
    mutation: { onSuccess: (_, { subjectId }) => settle('subjects.deleted', subjectId), onError: failure },
  });

  return {
    create: async (name) => {
      await createSubject.mutateAsync({ data: { name } });
    },
    rename: async (subjectId, name) => {
      await updateSubject.mutateAsync({ subjectId, data: { name } });
    },
    move: (subjectId, position) => {
      reorderSubject.mutate({ subjectId, data: { position } });
    },
    remove: (subjectId) => {
      deleteSubject.mutate({ subjectId });
    },
  };
}
