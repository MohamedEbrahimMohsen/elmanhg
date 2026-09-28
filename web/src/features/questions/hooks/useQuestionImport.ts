import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetLessonsQueryKey } from '@/shared/api/generated/lessons/lessons';
import type { ImportQuestionsResult, QuestionImportPreviewResult } from '@/shared/api/generated/model';
import { useImportQuestions, usePreviewQuestionImport } from '@/shared/api/generated/question-imports/question-imports';
import { getGetQuestionsQueryKey } from '@/shared/api/generated/questions/questions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface QuestionImport {
  preview: QuestionImportPreviewResult | undefined;
  imported: ImportQuestionsResult | undefined;
  check: (file: File) => Promise<void>;
  confirm: () => Promise<void>;
  isConfirming: boolean;
  reset: () => void;
}

export function useQuestionImport(lessonId: string): QuestionImport {
  const { t } = useTranslation('questions');
  const queryClient = useQueryClient();
  const [selection, setSelection] = useState<{ file: File; batchId: string } | null>(null);
  const previewMutation = usePreviewQuestionImport();
  const importMutation = useImportQuestions({
    mutation: {
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: getGetQuestionsQueryKey() });
        await queryClient.invalidateQueries({ queryKey: getGetLessonsQueryKey() });
        toast(t('import.toast.imported'));
      },
    },
  });

  return {
    preview: previewMutation.data,
    imported: importMutation.data,
    check: async (file) => {
      const batchId = selection?.file === file ? selection.batchId : crypto.randomUUID();
      setSelection({ file, batchId });
      importMutation.reset();
      await previewMutation.mutateAsync({ data: { lessonId, file } });
    },
    confirm: async () => {
      if (!selection) {
        return;
      }
      try {
        await importMutation.mutateAsync({ data: { lessonId, batchId: selection.batchId, file: selection.file } });
      } catch (error) {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      }
    },
    isConfirming: importMutation.isPending,
    reset: () => {
      previewMutation.reset();
      importMutation.reset();
      setSelection(null);
    },
  };
}
