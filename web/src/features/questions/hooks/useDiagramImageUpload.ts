import type { UploadDiagramImageResult } from '@/shared/api/generated/model';
import { useUploadDiagramImage } from '@/shared/api/generated/lessons/lessons';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface DiagramImageUpload {
  upload: (file: File) => Promise<UploadDiagramImageResult | null>;
  isPending: boolean;
  errorCode: string | null;
}

export function useDiagramImageUpload(lessonId: string): DiagramImageUpload {
  const mutation = useUploadDiagramImage();
  const errorCode = mutation.error
    ? mutation.error instanceof ApiError
      ? mutation.error.code
      : unhandledErrorCode
    : null;

  return {
    upload: (file) =>
      mutation.mutateAsync({ lessonId, data: { file } }).then(
        (result) => result,
        () => null,
      ),
    isPending: mutation.isPending,
    errorCode,
  };
}
