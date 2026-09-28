import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetLessonsQueryKey } from '@/shared/api/generated/lessons/lessons';
import type { QuestionDetailResult } from '@/shared/api/generated/model';
import {
  getGetQuestionQueryKey,
  getGetQuestionsQueryKey,
  useCreateQuestion,
  useResubmitQuestion,
  useUpdateQuestion,
} from '@/shared/api/generated/questions/questions';
import { toQuestionRequest } from '../api/questionValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export function useQuestionSave(
  lessonId: string,
  question: QuestionDetailResult | undefined,
): (values: QuestionValues) => Promise<void> {
  const { t } = useTranslation('questions');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const create = useCreateQuestion();
  const update = useUpdateQuestion();
  const resubmit = useResubmitQuestion();

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: getGetQuestionsQueryKey() });
    await queryClient.invalidateQueries({ queryKey: getGetLessonsQueryKey() });
    if (question) {
      await queryClient.invalidateQueries({ queryKey: getGetQuestionQueryKey(question.id) });
    }
  };

  return async (values) => {
    const data = toQuestionRequest(values);
    if (!question) {
      const { id } = await create.mutateAsync({ data: { lessonId, ...data } });
      toast(t('editor.created'));
      await invalidate();
      await navigate({ to: '/admin/question/$questionId', params: { questionId: id } });
      return;
    }
    if (question.validationStatus === 'Rejected') {
      await resubmit.mutateAsync({ questionId: question.id, data });
      toast(t('editor.resubmitted'));
    } else {
      await update.mutateAsync({ questionId: question.id, data });
      toast(t('editor.saved'));
    }
    await invalidate();
  };
}
