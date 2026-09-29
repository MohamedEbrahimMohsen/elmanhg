import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { getGetExamSessionQueryKey, useStartMultiUnitExam } from '@/shared/api/generated/exams/exams';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { invalidateExamViews } from '../api/invalidateExamViews';

export interface StartMultiExam {
  start: (subjectId: string, unitIds: string[], size: number) => void;
  isPending: boolean;
  errorCode: string | null;
  reset: () => void;
}

export function useStartMultiExam(): StartMultiExam {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useStartMultiUnitExam({
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
    start: (subjectId, unitIds, size) => {
      mutation.mutate({ subjectId, data: { unitIds, size } });
    },
    isPending: mutation.isPending,
    errorCode,
    reset: mutation.reset,
  };
}
