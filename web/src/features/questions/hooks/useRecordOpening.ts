import { useEffect, useEffectEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  getGetValidationQueueQueryKey,
  useRecordQuestionOpening,
} from '@/shared/api/generated/validation-queue/validation-queue';
import { ApiError } from '@/shared/lib/apiError';
import { isReviewSessionErrorCode, useReviewSession } from './useReviewSession';

export function useRecordOpening(
  reviewSessionId: string | undefined,
  question: { id: string; version: number } | undefined,
): void {
  const queryClient = useQueryClient();
  const { restart } = useReviewSession();
  const { mutate } = useRecordQuestionOpening({
    mutation: {
      onSuccess: () => queryClient.invalidateQueries({ queryKey: getGetValidationQueueQueryKey() }),
      onError: async (error) => {
        if (error instanceof ApiError && isReviewSessionErrorCode(error.code)) {
          await restart();
        }
      },
    },
  });
  const questionId = question?.id;
  const version = question?.version;
  const record = useEffectEvent((sessionId: string, openedQuestionId: string) => {
    mutate({ reviewSessionId: sessionId, questionId: openedQuestionId });
  });

  useEffect(() => {
    if (reviewSessionId !== undefined && questionId !== undefined && version !== undefined) {
      record(reviewSessionId, questionId);
    }
  }, [reviewSessionId, questionId, version]);
}
