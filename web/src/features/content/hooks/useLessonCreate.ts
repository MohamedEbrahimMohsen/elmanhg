import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { getGetLessonsQueryKey, useCreateLesson } from '@/shared/api/generated/lessons/lessons';
import { getGetSubjectQueryKey } from '@/shared/api/generated/subjects/subjects';
import { useContentFeedback } from './useContentFeedback';

export function useLessonCreate(subjectId: string): (unitId: string, name: string) => Promise<void> {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { success } = useContentFeedback();
  const createLesson = useCreateLesson({
    mutation: {
      onSuccess: async () => {
        success('lessons.created');
        await queryClient.invalidateQueries({ queryKey: getGetLessonsQueryKey() });
        await queryClient.invalidateQueries({ queryKey: getGetSubjectQueryKey(subjectId) });
      },
    },
  });

  return async (unitId, name) => {
    const { id } = await createLesson.mutateAsync({ data: { unitId, name } });
    await navigate({ to: '/admin/lesson/$lessonId', params: { lessonId: id } });
  };
}
