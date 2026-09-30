import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { trackFunnelEvent } from '@/features/analytics';
import { invalidateMastery } from '@/features/mastery';
import { paywallReason, type PaywallReason } from '@/features/subscription';
import type { SessionResult, SubmitAnswerRequest } from '@/shared/api/generated/model';
import { getGetSessionQueryKey, useSubmitSessionAnswer } from '@/shared/api/generated/sessions/sessions';
import { getGetMyUsageQueryKey } from '@/shared/api/generated/subscriptions/subscriptions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { mergeAnsweredItem } from '../api/quizSession';

export interface QuizSubmit {
  submit: (data: SubmitAnswerRequest) => void;
  isPending: boolean;
  paywall: PaywallReason | null;
  closePaywall: () => void;
}

export function useQuizSubmit(sessionId: string, onAnswered?: () => void): QuizSubmit {
  const { t } = useTranslation('quiz');
  const queryClient = useQueryClient();
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
        onAnswered?.();
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
    submit: (data) => {
      mutation.mutate({ sessionId, data });
    },
    isPending: mutation.isPending,
    paywall,
    closePaywall: () => {
      setPaywall(null);
    },
  };
}
