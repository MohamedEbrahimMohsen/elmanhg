import { describe, expect, it } from 'vitest';
import { examItem, openExam } from '@/test/examFixtures';
import { isCountdownUrgent, remainingMilliseconds, sortedExamItems, splitCountdown, unitIdOf } from './examSession';

describe('examSession', () => {
  it('sorts items by position', () => {
    const session = openExam([examItem(3), examItem(1), examItem(2)]);

    expect(sortedExamItems(session).map((item) => Number(item.position))).toEqual([1, 2, 3]);
  });

  it('subtracts the clock offset and never goes below zero', () => {
    const now = Date.parse('2026-09-28T10:00:00Z');

    expect(remainingMilliseconds('2026-09-28T10:30:00Z', 0, now)).toBe(1_800_000);
    expect(remainingMilliseconds('2026-09-28T10:30:00Z', 60_000, now)).toBe(1_740_000);
    expect(remainingMilliseconds('2026-09-28T10:30:00Z', 0, now + 3_600_000)).toBe(0);
  });

  it('splits the countdown rounding up to whole seconds', () => {
    expect(splitCountdown(61_001)).toEqual({ minutes: 1, seconds: 2 });
    expect(splitCountdown(0)).toEqual({ minutes: 0, seconds: 0 });
  });

  it('marks the last two minutes as urgent', () => {
    expect(isCountdownUrgent(120_000)).toBe(true);
    expect(isCountdownUrgent(120_001)).toBe(false);
  });

  it('returns the first unit id or null', () => {
    expect(unitIdOf(openExam([]))).toBe('34343434-3434-4343-8343-343434343434');
    expect(unitIdOf(openExam([], { units: [] }))).toBeNull();
  });
});
