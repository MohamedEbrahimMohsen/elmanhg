import { describe, expect, it } from 'vitest';
import { isPendingMathSteps, mathStepsAnswerOf, type MathStepsItem } from './mathStepsItem';

const answer = { steps: ['2x = 4'], finalAnswer: 'x = 2' };
const item = (overrides: Partial<MathStepsItem>): MathStepsItem => ({
  type: 'MathSteps',
  attempt: null,
  pendingAnswer: null,
  savedAnswer: null,
  ...overrides,
});

describe('mathStepsItem', () => {
  it('detects a pending math answer from pendingAnswer', () => {
    const pending = item({ pendingAnswer: answer });

    expect(isPendingMathSteps(pending)).toBe(true);
    expect(mathStepsAnswerOf(pending)).toEqual(answer);
  });

  it('detects a pending math answer from savedAnswer', () => {
    expect(isPendingMathSteps(item({ savedAnswer: answer }))).toBe(true);
  });

  it('is not pending with an attempt', () => {
    const attempt = {
      id: 'aaaaaaaa-aaaa-4aaa-8aaa-000000000001',
      answer,
      score: 2,
      normalisedScore: 1,
      outcome: 'Correct',
      awaitsReview: false,
      feedback: null,
      timeTakenMilliseconds: 4000,
      createdAt: '2026-09-28T10:00:05Z',
    };

    expect(isPendingMathSteps(item({ pendingAnswer: answer, attempt }))).toBe(false);
  });

  it('is not pending with a blank final answer', () => {
    expect(isPendingMathSteps(item({ pendingAnswer: { steps: ['2x = 4'], finalAnswer: '  ' } }))).toBe(false);
  });

  it('is not pending for another type', () => {
    expect(isPendingMathSteps(item({ type: 'Essay', pendingAnswer: answer }))).toBe(false);
  });
});
