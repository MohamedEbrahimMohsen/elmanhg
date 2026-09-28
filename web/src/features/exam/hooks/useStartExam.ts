import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { getGetExamSessionQueryKey, useStartUnitExam } from '@/shared/api/generated/exams/exams';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { invalidateExamViews } from '../api/invalidateExamViews';

export interface StartExam {
  start: () => void;
  isPending: boolean;
  errorCode: string | null;
}

export function useStartExam(unitId: string): StartExam {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useStartUnitExam({
    mutation: {
      onSuccess: async (session) => {
        queryClient.setQueryData(getGetExamSessionQueryKey(session.id), session);
        void invalidateExamViews(queryClient);
        const params = { sessionId: session.id };
        await (session.submittedAt
          ? navigate({ to: '/student/exam-result/$sessionId', params })
          : navigate({ to: '/student/exam/$sessionId', params }));
      },
    },
  });
  const errorCode = mutation.error
    ? mutation.error instanceof ApiError
      ? mutation.error.code
      : unhandledErrorCode
    : null;

  return {
    start: () => {
      mutation.mutate({ unitId });
    },
    isPending: mutation.isPending,
    errorCode,
  };
}
