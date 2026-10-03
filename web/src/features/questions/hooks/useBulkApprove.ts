import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetValidationQueueQueryKey,
  useBulkApproveQuestions,
} from '@/shared/api/generated/validation-queue/validation-queue';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { formatNumber } from '@/shared/lib/format';
import { isReviewSessionErrorCode, useReviewSession } from './useReviewSession';

export function useBulkApprove() {
  const { t, i18n } = useTranslation('questions');
  const queryClient = useQueryClient();
  const { restart } = useReviewSession();
  const mutation = useBulkApproveQuestions({
    mutation: {
      onSuccess: async (result) => {
        const count = Number(result.approvedCount);
        const formattedCount = formatNumber(count, i18n.resolvedLanguage ?? i18n.language);
        toast(t('validation.queue.bulkApproved', { count, formattedCount }));
        await queryClient.invalidateQueries({ queryKey: getGetValidationQueueQueryKey() });
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        if (isReviewSessionErrorCode(code)) {
          await restart();
        }
      },
    },
  });

  return {
    approve: (reviewSessionId: string, questionIds: string[]) =>
      new Promise<void>((resolve) => {
        mutation.mutate(
          { data: { reviewSessionId, questionIds } },
          {
            onSettled: () => {
              resolve();
            },
          },
        );
      }),
    isPending: mutation.isPending,
  };
}
