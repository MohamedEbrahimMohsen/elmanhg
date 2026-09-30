import { describe, expect, it } from 'vitest';
import { quizItem } from '@/test/quizFixtures';
import { choiceReview, describeCorrectAnswer } from './correctAnswer';
import { toQuizQuestion } from './quizItem';

describe('correctAnswer math steps', () => {
  it('describes the first accepted answer as math', () => {
    const question = toQuizQuestion(quizItem(1, { type: 'MathSteps', body: {} }));
    const spec = { acceptedAnswers: ['x = 2', '2'], form: 'equivalent' };

    expect(describeCorrectAnswer(question, spec)).toEqual({ kind: 'math', latex: 'x = 2' });
    expect(choiceReview(question, spec)).toBeUndefined();
  });
});
