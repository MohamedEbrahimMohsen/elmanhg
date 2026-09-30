import { describe, expect, it } from 'vitest';
import { answered, essayQuizItem, quizItem } from '@/test/quizFixtures';
import { essayAnswerText, hasPendingEssay, isWrittenEssay } from './essayItem';

describe('essayItem', () => {
  it('reads the essay text from the attempt, then the pending answer, then the saved answer', () => {
    const graded = answered(essayQuizItem(1, { pendingAnswer: { text: 'pending' } }), 'Correct', { text: 'attempt' });

    expect(essayAnswerText(graded)).toBe('attempt');
    expect(essayAnswerText(essayQuizItem(1, { pendingAnswer: { text: 'pending' } }))).toBe('pending');
    expect(essayAnswerText({ type: 'Essay', attempt: null, savedAnswer: { text: 'saved' } })).toBe('saved');
  });

  it('treats blank or non-essay items as not written', () => {
    expect(isWrittenEssay(essayQuizItem(1, { pendingAnswer: { text: '  ' } }))).toBe(false);
    expect(isWrittenEssay(quizItem(1, { pendingAnswer: { text: 'text' } }))).toBe(false);
    expect(isWrittenEssay(essayQuizItem(1, { pendingAnswer: { text: 'text' } }))).toBe(true);
  });

  it('finds a pending essay only while it has no attempt', () => {
    const pending = essayQuizItem(1, { pendingAnswer: { text: 'text' } });

    expect(hasPendingEssay([quizItem(2), pending])).toBe(true);
    expect(hasPendingEssay([quizItem(2), answered(pending, 'Correct', { text: 'text' })])).toBe(false);
  });
});
