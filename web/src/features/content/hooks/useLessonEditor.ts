import { useQueryClient } from '@tanstack/react-query';
import {
  getGetLessonQueryKey,
  getGetLessonsQueryKey,
  useUpdateLesson,
  useUploadLessonImage,
} from '@/shared/api/generated/lessons/lessons';
import { toUpdateLessonRequest } from '../api/lessonValues';
import type { LessonValues } from '../schemas/lessonSchema';
import { useContentFeedback } from './useContentFeedback';

export interface LessonEditor {
  save: (values: LessonValues) => Promise<void>;
  uploadImage: (file: File) => Promise<string>;
}

export function useLessonEditor(lessonId: string): LessonEditor {
  const queryClient = useQueryClient();
  const { success } = useContentFeedback();
  const updateLesson = useUpdateLesson({
    mutation: {
      onSuccess: async () => {
        success('lessonEditor.saved');
        await queryClient.invalidateQueries({ queryKey: getGetLessonQueryKey(lessonId) });
        await queryClient.invalidateQueries({ queryKey: getGetLessonsQueryKey() });
      },
    },
  });
  const uploadLessonImage = useUploadLessonImage({
    mutation: {
      onSuccess: () => {
        success('lessonEditor.image.uploaded');
      },
    },
  });

  return {
    save: async (values) => {
      await updateLesson.mutateAsync({ lessonId, data: toUpdateLessonRequest(values) });
    },
    uploadImage: async (file) => (await uploadLessonImage.mutateAsync({ lessonId, data: { file } })).url,
  };
}
