import { describe, expect, it } from 'vitest';
import type { AvatarTurn } from '@/shared/api/generated/model';
import { contextKey, historyFor, toSendRequest } from './avatarContext';

const turns: AvatarTurn[] = [
  { role: 'User', content: 'q1' },
  { role: 'Assistant', content: 'a1' },
  { role: 'User', content: 'q2' },
  { role: 'Assistant', content: 'a2' },
];

describe('avatarContext', () => {
  it('builds the same key for the same context', () => {
    expect(contextKey({ entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' })).toBe(
      contextKey({ entryPoint: 'Lesson', lessonId: 'l1', title: 'Another title' }),
    );
  });

  it('builds a different key for another question', () => {
    expect(contextKey({ entryPoint: 'QuizQuestion', sessionId: 's1', questionId: 'q1' })).not.toBe(
      contextKey({ entryPoint: 'QuizQuestion', sessionId: 's1', questionId: 'q2' }),
    );
  });

  it('keeps only the last completed turns', () => {
    expect(historyFor(turns, 2)).toEqual([
      { role: 'User', content: 'q2' },
      { role: 'Assistant', content: 'a2' },
    ]);
    expect(historyFor(turns, 10)).toEqual(turns);
    expect(historyFor(turns, 0)).toEqual([]);
  });

  it('sends missing ids as null and trims the message', () => {
    expect(toSendRequest({ entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' }, [], '  why?  ')).toEqual({
      entryPoint: 'Lesson',
      lessonId: 'l1',
      sessionId: null,
      questionId: null,
      history: [],
      message: 'why?',
    });
  });
});
