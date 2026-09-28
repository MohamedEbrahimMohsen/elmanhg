import { useUploadLessonImage } from '@/shared/api/generated/lessons/lessons';

export function useQuestionImageUpload(lessonId: string): (file: File) => Promise<string> {
  const upload = useUploadLessonImage();

  return async (file) => (await upload.mutateAsync({ lessonId, data: { file } })).url;
}
