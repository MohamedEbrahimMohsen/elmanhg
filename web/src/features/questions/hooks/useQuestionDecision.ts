import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { QuestionDifficulty } from '@/shared/api/generated/model';
import {
  getGetValidationQuestionQueryKey,
  getGetValidationQueueQueryKey,
  useApproveQuestion,
  useRejectQuestion,
} from '@/shared/api/generated/validation-queue/validation-queue';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface QuestionDecisionTarget {
  questionId: string;
  version: number;
}

export function useQuestionDecision() {
  const { t } = useTranslation('questions');
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const onDecided = async (questionId: string, messageKey: string) => {
    toast(t(messageKey));
    await queryClient.invalidateQueries({ queryKey: getGetValidationQueueQueryKey() });
    await queryClient.invalidateQueries({ queryKey: getGetValidationQuestionQueryKey(questionId) });
    await navigate({ to: '/teacher' });
  };
  const onFailed = async (questionId: string, error: unknown) => {
    const code = error instanceof ApiError ? error.code : unhandledErrorCode;
    toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
    if (code === 'QUESTION_VERSION_CHANGED') {
      await queryClient.invalidateQueries({ queryKey: getGetValidationQuestionQueryKey(questionId) });
    }
  };

  const approveMutation = useApproveQuestion({
    mutation: {
      onSuccess: (_, { questionId }) => onDecided(questionId, 'validation.decision.approved'),
      onError: (error, { questionId }) => onFailed(questionId, error),
    },
  });
  const rejectMutation = useRejectQuestion({
    mutation: {
      onSuccess: (_, { questionId }) => onDecided(questionId, 'validation.decision.rejected'),
      onError: (error, { questionId }) => onFailed(questionId, error),
    },
  });

  return {
    approve: async ({
      questionId,
      version,
      difficulty,
    }: QuestionDecisionTarget & { difficulty: QuestionDifficulty }) => {
      await approveMutation.mutateAsync({ questionId, data: { version, difficulty } }).catch(() => undefined);
    },
    reject: async ({ questionId, version, reason }: QuestionDecisionTarget & { reason: string }) => {
      await rejectMutation.mutateAsync({ questionId, data: { version, reason } }).catch(() => undefined);
    },
    isPending: approveMutation.isPending || rejectMutation.isPending,
  };
}
