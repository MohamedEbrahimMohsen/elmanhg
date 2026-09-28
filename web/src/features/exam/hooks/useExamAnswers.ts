import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { emptyAnswer, toAnswerPayload, type QuestionAnswer } from '@/features/questions';
import { fromAnswerPayload, isAnswerEmpty, toQuizQuestion } from '@/features/quiz';
import { useSaveExamAnswer } from '@/shared/api/generated/exams/exams';
import type { ExamItemResult, ExamSessionResult } from '@/shared/api/generated/model';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { examAutoSaveDelayMilliseconds } from '../api/examSession';

export type ExamSaveStatus = 'idle' | 'saving' | 'saved' | 'error';

export interface ExamAnswers {
  answerOf: (questionId: string) => QuestionAnswer;
  change: (item: ExamItemResult, answer: QuestionAnswer) => void;
  flush: () => Promise<void>;
  status: ExamSaveStatus;
  lastSavedAt: Date | null;
  unansweredCount: number;
}

const timeOverCodes = new Set(['EXAM_TIME_EXPIRED', 'SESSION_ALREADY_SUBMITTED']);

function initialAnswers(session: ExamSessionResult): Record<string, QuestionAnswer> {
  return Object.fromEntries(
    session.items.map((item) => [
      item.questionId,
      item.savedAnswer ? fromAnswerPayload(toQuizQuestion(item), item.savedAnswer) : emptyAnswer(),
    ]),
  );
}

export function useExamAnswers(session: ExamSessionResult, onTimeOver: () => void): ExamAnswers {
  const { t } = useTranslation('exam');
  const [answers, setAnswers] = useState(() => initialAnswers(session));
  const [status, setStatus] = useState<ExamSaveStatus>('idle');
  const [lastSavedAt, setLastSavedAt] = useState<Date | null>(null);
  const latest = useRef(answers);
  const timers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const inFlight = useRef(new Map<string, Promise<void>>());
  const mutation = useSaveExamAnswer();

  useEffect(() => {
    const pending = timers.current;
    return () => {
      pending.forEach((timer) => {
        clearTimeout(timer);
      });
      pending.clear();
    };
  }, []);

  const answerOf = (questionId: string): QuestionAnswer => answers[questionId] ?? emptyAnswer();

  const save = async (item: ExamItemResult): Promise<void> => {
    setStatus('saving');
    const answer = latest.current[item.questionId] ?? emptyAnswer();
    try {
      const data = { answer: toAnswerPayload(toQuizQuestion(item), answer) };
      const result = await mutation.mutateAsync({ sessionId: session.id, questionId: item.questionId, data });
      setStatus('saved');
      setLastSavedAt(new Date(result.answerSavedAt));
    } catch (error) {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      if (timeOverCodes.has(code)) {
        onTimeOver();
        return;
      }
      setStatus('error');
      toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
    }
  };

  const send = (questionId: string): void => {
    timers.current.delete(questionId);
    const item = session.items.find((candidate) => candidate.questionId === questionId);
    if (!item) {
      return;
    }
    const previous = inFlight.current.get(questionId) ?? Promise.resolve();
    inFlight.current.set(
      questionId,
      previous.then(() => save(item)),
    );
  };

  return {
    answerOf,
    change: (item, answer) => {
      latest.current = { ...latest.current, [item.questionId]: answer };
      setAnswers(latest.current);
      const pending = timers.current.get(item.questionId);
      if (pending !== undefined) {
        clearTimeout(pending);
      }
      timers.current.set(
        item.questionId,
        setTimeout(() => {
          send(item.questionId);
        }, examAutoSaveDelayMilliseconds),
      );
    },
    flush: async () => {
      [...timers.current].forEach(([questionId, timer]) => {
        clearTimeout(timer);
        send(questionId);
      });
      await Promise.all(inFlight.current.values());
    },
    status,
    lastSavedAt,
    unansweredCount: session.items.filter((item) => isAnswerEmpty(toQuizQuestion(item), answerOf(item.questionId)))
      .length,
  };
}
