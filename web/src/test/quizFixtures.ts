import type { AttemptResult, SessionItemResult, SessionResult } from '@/shared/api/generated/model';

export const quizSessionId = '11111111-1111-4111-8111-111111111111';
export const quizLessonId = '55555555-5555-4555-8555-555555555555';

const scores = { Correct: 1, Partial: 0.5, Incorrect: 0 } as const;

export function quizItem(position: number, overrides?: Partial<SessionItemResult>): SessionItemResult {
  return {
    position,
    questionId: `00000000-0000-4000-8000-00000000000${String(position)}`,
    questionVersion: 1,
    type: 'Mcq',
    stem: `<p>Question ${String(position)} stem</p>`,
    body: {
      options: [
        { id: 'a', text: '<p>3</p>' },
        { id: 'b', text: '<p>4</p>' },
        { id: 'c', text: '<p>5</p>' },
      ],
    },
    maxScore: 1,
    attempt: null,
    correctAnswer: null,
    explanation: null,
    pendingAnswer: null,
    ...overrides,
  };
}

export function essayQuizItem(position: number, overrides?: Partial<SessionItemResult>): SessionItemResult {
  return quizItem(position, { type: 'Essay', body: { maxWords: 5 }, maxScore: 5, ...overrides });
}

export function answered(
  item: SessionItemResult,
  outcome: 'Correct' | 'Partial' | 'Incorrect',
  answer: object,
  overrides?: Partial<AttemptResult>,
): SessionItemResult {
  return {
    ...item,
    attempt: {
      id: `aaaaaaaa-aaaa-4aaa-8aaa-00000000000${String(item.position)}`,
      answer,
      score: scores[outcome],
      normalisedScore: scores[outcome],
      outcome,
      feedback: null,
      timeTakenMilliseconds: 4000,
      createdAt: '2026-09-28T10:00:05Z',
      ...overrides,
    },
    correctAnswer: { correctOptionId: 'b' },
    explanation: '<p>Two plus two is four.</p>',
  };
}

export function quizSession(items: SessionItemResult[], overrides?: Partial<SessionResult>): SessionResult {
  const unanswered = items
    .filter((item) => item.attempt === null && item.pendingAnswer === null)
    .map((item) => Number(item.position));
  return {
    id: quizSessionId,
    kind: 'Quiz',
    scope: { lessonId: quizLessonId },
    isTestMode: false,
    startedAt: '2026-09-28T10:00:00Z',
    submittedAt: null,
    scorePercent: null,
    timeTakenMilliseconds: 0,
    currentPosition: unanswered.length > 0 ? Math.min(...unanswered) : null,
    items,
    ...overrides,
  };
}
