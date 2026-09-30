import { useGetEssayGrade } from '@/shared/api/generated/sessions/sessions';

export const essayGradePollIntervalMs = 2000;

export function useEssayGrade(sessionId: string, questionId: string) {
  return useGetEssayGrade(sessionId, questionId, {
    query: {
      refetchInterval: (query) => (query.state.data?.status === 'Pending' ? essayGradePollIntervalMs : false),
    },
  });
}
