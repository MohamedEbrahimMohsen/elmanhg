import { useEffect, useRef } from 'react';
import { useGetMathStepGrade } from '@/shared/api/generated/sessions/sessions';

export const mathStepGradePollIntervalMs = 2000;

export function useMathStepGrade(sessionId: string, questionId: string, onGraded?: () => void) {
  const query = useGetMathStepGrade(sessionId, questionId, {
    query: {
      retry: false,
      refetchInterval: (query) => (query.state.data?.status === 'Pending' ? mathStepGradePollIntervalMs : false),
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
