import { useQueryClient } from '@tanstack/react-query';
import {
  useArchiveLesson,
  useDeleteLesson,
  usePublishLesson,
  useReorderLesson,
  useUnpublishLesson,
} from '@/shared/api/generated/lessons/lessons';
import { isContentQuery } from '../api/contentQueries';
import { useContentFeedback } from './useContentFeedback';

export interface LessonMutations {
  publish: (lessonId: string) => void;
  unpublish: (lessonId: string) => void;
  archive: (lessonId: string) => void;
  move: (lessonId: string, position: number) => void;
  remove: (lessonId: string, onDeleted?: () => void) => void;
}

export function useLessonMutations(): LessonMutations {
  const queryClient = useQueryClient();
  const { success, failure } = useContentFeedback();
  const settle = async (key: string, excludedLessonId?: string) => {
    success(key);
    await queryClient.invalidateQueries({ predicate: (query) => isContentQuery(query.queryKey, excludedLessonId) });
  };
  const publishLesson = usePublishLesson({
    mutation: { onSuccess: () => settle('lessons.published'), onError: failure },
  });
  const unpublishLesson = useUnpublishLesson({
    mutation: { onSuccess: () => settle('lessons.unpublished'), onError: failure },
  });
  const archiveLesson = useArchiveLesson({
    mutation: { onSuccess: () => settle('lessons.archived'), onError: failure },
  });
  const reorderLesson = useReorderLesson({ mutation: { onSuccess: () => settle('lessons.moved'), onError: failure } });
  const deleteLesson = useDeleteLesson({
    mutation: { onSuccess: (_data, variables) => settle('lessons.deleted', variables.lessonId), onError: failure },
  });

  return {
    publish: (lessonId) => {
      publishLesson.mutate({ lessonId });
    },
    unpublish: (lessonId) => {
      unpublishLesson.mutate({ lessonId });
    },
    archive: (lessonId) => {
      archiveLesson.mutate({ lessonId });
    },
    move: (lessonId, position) => {
      reorderLesson.mutate({ lessonId, data: { position } });
    },
    remove: (lessonId, onDeleted) => {
      deleteLesson.mutate(
        { lessonId },
        {
          onSuccess: () => {
            onDeleted?.();
          },
        },
      );
    },
  };
}
