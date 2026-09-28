import { describe, expect, it } from 'vitest';
import { answered, quizItem, quizSession } from '@/test/quizFixtures';
import {
  defaultQuizSize,
  initialPosition,
  lessonIdOf,
  mergeAnsweredItem,
  nextQuizSize,
  quizSizes,
  splitDuration,
} from './quizSession';

describe('quizSession', () => {
  it('offers the configured quiz sizes', () => {
    expect(quizSizes).toEqual([5, 10, 20]);
    expect(defaultQuizSize).toBe(10);
  });

  it('merges an answered item and moves to the lowest unanswered position', () => {
    const session = quizSession([quizItem(1), quizItem(2), quizItem(3)]);
    const item1 = answered(quizItem(1), 'Correct', { optionId: 'b' });

    const merged = mergeAnsweredItem(session, item1);

    expect(merged.items[0]).toEqual(item1);
    expect(merged.currentPosition).toBe(2);
    expect(merged.items.slice(1)).toEqual([quizItem(2), quizItem(3)]);
    expect(session.items[0]?.attempt).toBeNull();
    expect(session.currentPosition).toBe(1);
  });

  it('clears the current position when every item is answered', () => {
    const session = quizSession([answered(quizItem(1), 'Correct', { optionId: 'b' }), quizItem(2)]);

    const merged = mergeAnsweredItem(session, answered(quizItem(2), 'Incorrect', { optionId: 'a' }));

    expect(merged.currentPosition).toBeNull();
  });

  it('starts at the current position, or at the last item when all are answered', () => {
    expect(initialPosition(quizSession([quizItem(1), quizItem(2), quizItem(3)], { currentPosition: 2 }))).toBe(2);
    expect(initialPosition(quizSession([quizItem(3), quizItem(1), quizItem(2)], { currentPosition: null }))).toBe(3);
  });

  it('picks the smallest size that covers the served count', () => {
    expect([3, 5, 7, 10, 12, 20].map((count) => nextQuizSize(count))).toEqual([5, 5, 10, 10, 20, 20]);
  });

  it('reads the lesson id from the quiz scope', () => {
    expect(lessonIdOf(quizSession([quizItem(1)], { scope: { lessonId: 'x' } }))).toBe('x');
    expect(lessonIdOf(quizSession([quizItem(1)], { scope: {} }))).toBeNull();
  });

  it('splits a duration into minutes and seconds', () => {
    expect(splitDuration(0)).toEqual({ minutes: 0, seconds: 0 });
    expect(splitDuration(65_000)).toEqual({ minutes: 1, seconds: 5 });
    expect(splitDuration(59_600)).toEqual({ minutes: 1, seconds: 0 });
    expect(splitDuration(3_600_000)).toEqual({ minutes: 60, seconds: 0 });
  });
});
