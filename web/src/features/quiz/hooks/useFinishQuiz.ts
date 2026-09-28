import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetSessionQueryKey, useFinishSession } from '@/shared/api/generated/sessions/sessions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface FinishQuiz {
  finish: () => void;
  isPending: boolean;
}

export function useFinishQuiz(sessionId: string): FinishQuiz {
  const { t } = useTranslation('quiz');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useFinishSession({
    mutation: {
      onSuccess: async (session) => {
        queryClient.setQueryData(getGetSessionQueryKey(sessionId), session);
        await navigate({ to: '/student/quiz-result/$sessionId', params: { sessionId } });
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        await queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey(sessionId) });
      },
    },
  });

  return {
    finish: () => {
      mutation.mutate({ sessionId });
    },
    isPending: mutation.isPending,
  };
}
