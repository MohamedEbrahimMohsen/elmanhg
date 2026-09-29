import { describe, expect, it } from 'vitest';
import { contextKey, toSendRequest } from './avatarContext';

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

  it('sends missing ids as null and trims the message', () => {
    expect(toSendRequest({ entryPoint: 'Lesson', lessonId: 'l1', title: 'Ohm' }, null, '  why?  ')).toEqual({
      entryPoint: 'Lesson',
      lessonId: 'l1',
      sessionId: null,
      questionId: null,
      conversationId: null,
      message: 'why?',
    });
  });

  it('sends the conversation id when continuing', () => {
    expect(toSendRequest({ entryPoint: 'Global' }, 'c1', 'next')).toMatchObject({ conversationId: 'c1' });
  });
});
