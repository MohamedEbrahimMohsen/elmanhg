import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { invalidateMastery } from '@/features/mastery';
import { isOverWordLimit, type StudentQuestion } from '@/features/questions';
import { useSession } from '@/features/session';
import type { PaywallReason } from '@/features/subscription';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { getGetSessionQueryKey } from '@/shared/api/generated/sessions/sessions';
import { useEssayDraft, type EssayDraftStatusValue } from './useEssayDraft';
import { useQuizSubmit } from './useQuizSubmit';

export type EssayProblem = 'required' | 'overLimit' | null;

export interface QuizEssayState {
  text: string;
  change: (text: string) => void;
  draftStatus: EssayDraftStatusValue;
  problem: EssayProblem;
  submit: () => void;
  isSubmitting: boolean;
  paywall: PaywallReason | null;
  closePaywall: () => void;
  refreshSession: () => void;
}

export function useQuizEssay(sessionId: string, item: SessionItemResult, question: StudentQuestion): QuizEssayState {
  const queryClient = useQueryClient();
  const studentId = useSession()?.userId ?? 'anonymous';
  const draft = useEssayDraft({ studentId, sessionId, questionId: item.questionId });
  const [shownAt] = useState(() => Date.now());
  const [problem, setProblem] = useState<EssayProblem>(null);
  const submitter = useQuizSubmit(sessionId, draft.discard);

  return {
    text: draft.text,
    change: (text) => {
      draft.change(text);
      setProblem(null);
    },
    draftStatus: draft.status,
    problem,
    submit: () => {
      if (draft.text.trim() === '') {
        setProblem('required');
        return;
      }
      if (isOverWordLimit(question.maxWords, draft.text)) {
        setProblem('overLimit');
        return;
      }
      submitter.submit({
        questionId: item.questionId,
        answer: { text: draft.text },
        timeTakenMilliseconds: Math.max(0, Date.now() - shownAt),
      });
    },
    isSubmitting: submitter.isPending,
    paywall: submitter.paywall,
    closePaywall: submitter.closePaywall,
    refreshSession: () => {
      void queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey(sessionId) });
      void invalidateMastery(queryClient);
    },
  };
}
