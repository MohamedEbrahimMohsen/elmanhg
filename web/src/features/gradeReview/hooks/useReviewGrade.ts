import { useQueryClient } from '@tanstack/react-query';
import type { UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetEssayGradeReviewQueryKey,
  getGetGradeReviewQueueQueryKey,
  getGetGradeReviewSubjectsQueryKey,
  getGetMathStepGradeReviewQueryKey,
  useReviewEssayGrade,
  useReviewMathStepGrade,
} from '@/shared/api/generated/grade-reviews/grade-reviews';
import { ApiError } from '@/shared/lib/apiError';
import type { GradeReviewKindValue } from '../api/gradeReviewOptions';
import { reviewErrorFields } from '../api/reviewErrorFields';
import { toReviewRequest } from '../api/toReviewRequest';
import type { GradeReviewFormValues } from '../schemas/gradeReviewFormSchema';

export function useReviewGrade(kind: GradeReviewKindValue, subjectId: string, gradeId: string) {
  const { t } = useTranslation('gradeReview');
  const queryClient = useQueryClient();
  const essay = useReviewEssayGrade();
  const mathSteps = useReviewMathStepGrade();
  const detailKey =
    kind === 'Essay'
      ? getGetEssayGradeReviewQueryKey(subjectId, gradeId)
      : getGetMathStepGradeReviewQueryKey(subjectId, gradeId);

  const send = (values: GradeReviewFormValues) => {
    const data = toReviewRequest(values);
    return kind === 'Essay'
      ? essay.mutateAsync({ subjectId, essayGradeId: gradeId, data })
      : mathSteps.mutateAsync({ subjectId, mathStepGradeId: gradeId, data });
  };

  const onFailed = (error: unknown, form: Pick<UseFormReturn<GradeReviewFormValues>, 'setError'>) => {
    const code = error instanceof ApiError ? error.code : null;
    const field = code === null ? undefined : reviewErrorFields[code];
    if (code !== null && field !== undefined) {
      form.setError(field, { type: 'server', message: `errors.${code}` }, { shouldFocus: true });
      return;
    }
    if (error instanceof ApiError && error.status === 409) {
      toast.error(t('detail.conflict'));
      void queryClient.invalidateQueries({ queryKey: detailKey });
      return;
    }
    toast.error(t('detail.saveFailed'));
  };

  return {
    submit: async (
      values: GradeReviewFormValues,
      form: Pick<UseFormReturn<GradeReviewFormValues>, 'setError'>,
    ): Promise<boolean> => {
      try {
        await send(values);
      } catch (error) {
        onFailed(error, form);
        return false;
      }
      await queryClient.invalidateQueries({ queryKey: getGetGradeReviewQueueQueryKey(subjectId) });
      await queryClient.invalidateQueries({ queryKey: getGetGradeReviewSubjectsQueryKey() });
      await queryClient.invalidateQueries({ queryKey: detailKey });
      toast.success(t('detail.saved'));
      return true;
    },
    isPending: essay.isPending || mathSteps.isPending,
  };
}
