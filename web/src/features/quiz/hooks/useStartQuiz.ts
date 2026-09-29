import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { getGetSessionQueryKey, useStartQuizSession } from '@/shared/api/generated/sessions/sessions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface StartQuiz {
  start: (questionCount: number) => void;
  isPending: boolean;
  errorCode: string | null;
  reset: () => void;
}

export function useStartQuiz(lessonId: string): StartQuiz {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useStartQuizSession({
    mutation: {
      onSuccess: async (session) => {
        queryClient.setQueryData(getGetSessionQueryKey(session.id), session);
        await navigate({ to: '/student/quiz/$sessionId', params: { sessionId: session.id } });
      },
    },
  });
  const errorCode = mutation.error
    ? mutation.error instanceof ApiError
      ? mutation.error.code
      : unhandledErrorCode
    : null;

  return {
    start: (questionCount) => {
      mutation.mutate({ data: { lessonId, questionCount } });
    },
    isPending: mutation.isPending,
    errorCode,
    reset: mutation.reset,
  };
}
