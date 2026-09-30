import { useEffect, useRef } from 'react';
import { useGetEssayGrade } from '@/shared/api/generated/sessions/sessions';

export const essayGradePollIntervalMs = 2000;

export function useEssayGrade(sessionId: string, questionId: string, onGraded?: () => void) {
  const query = useGetEssayGrade(sessionId, questionId, {
    query: {
      refetchInterval: (query) => (query.state.data?.status === 'Pending' ? essayGradePollIntervalMs : false),
    },
  });
  const status = query.data?.status;
  const previous = useRef(status);
  useEffect(() => {
    if (previous.current === 'Pending' && status === 'Graded') {
      onGraded?.();
    }
    previous.current = status;
  }, [status, onGraded]);
  return query;
}
