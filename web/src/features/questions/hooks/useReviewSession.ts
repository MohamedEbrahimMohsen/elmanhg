import { useQuery, useQueryClient } from '@tanstack/react-query';
import { startReviewSession } from '@/shared/api/generated/validation-queue/validation-queue';

export const reviewSessionErrorCodes = ['REVIEW_SESSION_EXPIRED', 'REVIEW_SESSION_NOT_FOUND'] as const;

const reviewSessionQueryKey = ['questions', 'review-session'] as const;

export function isReviewSessionErrorCode(code: string): boolean {
  return (reviewSessionErrorCodes as readonly string[]).includes(code);
}

export function useReviewSession() {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: reviewSessionQueryKey,
    queryFn: ({ signal }) => startReviewSession({ signal }),
    staleTime: Infinity,
    gcTime: Infinity,
    refetchOnWindowFocus: false,
    retry: false,
  });

  return {
    reviewSessionId: query.data?.reviewSessionId,
    query,
    restart: () => queryClient.resetQueries({ queryKey: reviewSessionQueryKey }),
  };
}
