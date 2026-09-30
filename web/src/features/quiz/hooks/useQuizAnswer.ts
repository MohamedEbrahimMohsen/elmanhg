import { useEffect, useState } from 'react';
import { clearMathDraft, type MathDraftOwner } from '@/features/mathSteps';
import { emptyAnswer, toAnswerPayload, type QuestionAnswer, type StudentQuestion } from '@/features/questions';
import type { PaywallReason } from '@/features/subscription';
import type { SessionItemResult } from '@/shared/api/generated/model';
import { isAnswerEmpty } from '../api/quizItem';
import { useQuizSubmit } from './useQuizSubmit';

export interface QuizAnswerState {
  answer: QuestionAnswer;
  setAnswer: (answer: QuestionAnswer) => void;
  check: () => void;
  isChecking: boolean;
  showRequired: boolean;
  paywall: PaywallReason | null;
  closePaywall: () => void;
}

export function useQuizAnswer(
  sessionId: string,
  item: SessionItemResult,
  question: StudentQuestion,
  draftOwner?: MathDraftOwner,
): QuizAnswerState {
  const [answer, setAnswerState] = useState(emptyAnswer);
  const [shownAt] = useState(() => Date.now());
  const [showRequired, setShowRequired] = useState(false);
  const submitter = useQuizSubmit(sessionId);
  const isRecorded = item.attempt !== null;
  const mathOwner = question.type === 'MathSteps' ? draftOwner : undefined;

  useEffect(() => {
    if (isRecorded && mathOwner) {
      clearMathDraft(mathOwner);
    }
  }, [isRecorded, mathOwner]);

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
      submitter.submit({
        questionId: item.questionId,
        answer: toAnswerPayload(question, answer),
        timeTakenMilliseconds: Math.max(0, Date.now() - shownAt),
      });
    },
    isChecking: submitter.isPending,
    showRequired,
    paywall: submitter.paywall,
    closePaywall: submitter.closePaywall,
  };
}
