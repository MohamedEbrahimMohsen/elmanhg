import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { trackFunnelEvent } from '@/features/analytics';
import { invalidateMastery } from '@/features/mastery';
import { emptyAnswer, toAnswerPayload, type QuestionAnswer, type StudentQuestion } from '@/features/questions';
import { paywallReason, type PaywallReason } from '@/features/subscription';
import type { SessionItemResult, SessionResult } from '@/shared/api/generated/model';
import { getGetSessionQueryKey, useSubmitSessionAnswer } from '@/shared/api/generated/sessions/sessions';
import { getGetMyUsageQueryKey } from '@/shared/api/generated/subscriptions/subscriptions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { isAnswerEmpty } from '../api/quizItem';
import { mergeAnsweredItem } from '../api/quizSession';

export interface QuizAnswerState {
  answer: QuestionAnswer;
  setAnswer: (answer: QuestionAnswer) => void;
  check: () => void;
  isChecking: boolean;
  showRequired: boolean;
  paywall: PaywallReason | null;
  closePaywall: () => void;
}

export function useQuizAnswer(sessionId: string, item: SessionItemResult, question: StudentQuestion): QuizAnswerState {
  const { t } = useTranslation('quiz');
  const queryClient = useQueryClient();
  const [answer, setAnswerState] = useState(emptyAnswer);
  const [shownAt] = useState(() => Date.now());
  const [showRequired, setShowRequired] = useState(false);
  const [paywall, setPaywall] = useState<PaywallReason | null>(null);
  const key = getGetSessionQueryKey(sessionId);
  const mutation = useSubmitSessionAnswer({
    mutation: {
      onSuccess: (answered) => {
        queryClient.setQueryData<SessionResult>(key, (old) =>
          old === undefined ? old : mergeAnsweredItem(old, answered),
        );
        void invalidateMastery(queryClient);
        void queryClient.invalidateQueries({ queryKey: getGetMyUsageQueryKey() });
        trackFunnelEvent('FirstQuizAnswered', { once: true });
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        const reason = paywallReason(code);
        if (reason) {
          setPaywall(reason);
        } else {
          toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        }
        await queryClient.invalidateQueries({ queryKey: key });
        await queryClient.invalidateQueries({ queryKey: getGetMyUsageQueryKey() });
      },
    },
  });

  return {
    answer,
    setAnswer: (next) => {
      setAnswerState(next);
      setShowRequired(false);
    },
    check: () => {
      if (isAnswerEmpty(question, answer)) {
        setShowRequired(true);
        return;
      }
      mutation.mutate({
        sessionId,
        data: {
          questionId: item.questionId,
          answer: toAnswerPayload(question, answer),
          timeTakenMilliseconds: Math.max(0, Date.now() - shownAt),
        },
      });
    },
    isChecking: mutation.isPending,
    showRequired,
    paywall,
    closePaywall: () => {
      setPaywall(null);
    },
  };
}
