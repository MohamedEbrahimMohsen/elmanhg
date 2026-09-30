import { describe, expect, it } from 'vitest';
import { emptyAnswer } from '@/features/questions';
import { quizItem } from '@/test/quizFixtures';
import { fromAnswerPayload, isAnswerEmpty, toQuizQuestion } from './quizItem';

const question = toQuizQuestion(quizItem(1, { type: 'MathSteps', body: {} }));

describe('quizItem math steps', () => {
  it('reads a math answer payload', () => {
    const payload = { steps: ['2x = 4'], finalAnswer: 'x = 2' };

    expect(fromAnswerPayload(question, payload).math).toEqual(payload);
    expect(fromAnswerPayload(question, { steps: 'x' })).toEqual(emptyAnswer());
  });

  it('treats a blank final answer as empty', () => {
    expect(isAnswerEmpty(question, { ...emptyAnswer(), math: { steps: ['2x = 4'], finalAnswer: '  ' } })).toBe(true);
    expect(isAnswerEmpty(question, { ...emptyAnswer(), math: { steps: [], finalAnswer: 'x=2' } })).toBe(false);
  });

  it('accepts a MathSteps item', () => {
    expect(question.type).toBe('MathSteps');
  });
});
