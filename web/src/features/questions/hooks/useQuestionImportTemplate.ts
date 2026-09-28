import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getQuestionImportTemplate } from '@/shared/api/generated/question-imports/question-imports';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { downloadBlob, questionImportTemplateFileName } from '../api/downloadBlob';

export function useQuestionImportTemplate(): { download: () => void; isPending: boolean } {
  const { t } = useTranslation('questions');
  const mutation = useMutation({
    mutationFn: () => getQuestionImportTemplate(),
    onSuccess: (blob) => {
      downloadBlob(blob, questionImportTemplateFileName);
    },
    onError: (error) => {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
    },
  });

  return {
    download: () => {
      mutation.mutate();
    },
    isPending: mutation.isPending,
  };
}
