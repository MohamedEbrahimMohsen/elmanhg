import { describe, expect, it } from 'vitest';
import { formatDateTime, formatRelativeTime, isRecent } from './dateTime';

const arabicIndicDigit = /[٠-٩۰-۹]/u;
const local = new Date(2026, 9, 3, 14, 37);
const normalise = (text: string) => text.replace(/\s/gu, ' ');
const hourMs = 3_600_000;

describe('formatDateTime', () => {
  it('formats an Arabic date and time with Latin digits', () => {
    expect(normalise(formatDateTime(local, 'ar'))).toBe('3 أكتوبر 2026، 2:37 م');
  });

  it('formats an English date and time', () => {
    expect(normalise(formatDateTime(local, 'en'))).toBe('Oct 3, 2026, 2:37 PM');
  });

  it('formats date only, time only and day styles', () => {
    expect(normalise(formatDateTime(local, 'ar', 'date'))).toBe('3 أكتوبر 2026');
    expect(normalise(formatDateTime(local, 'ar', 'time'))).toBe('2:37 م');
    expect(formatDateTime('2026-09-30', 'en', 'day')).toBe('Sep 30');
    expect(normalise(formatDateTime('2026-09-30', 'ar', 'day'))).toBe('30 سبتمبر');
  });

  it('formats a date-only string in UTC', () => {
    expect(formatDateTime('2026-06-01', 'en', 'date')).toBe('Jun 1, 2026');
  });

  it('formats the full deadline with the weekday', () => {
    expect(normalise(formatDateTime(local, 'en', 'fullDateTime'))).toBe('Saturday, October 3, 2026, 2:37 PM');
  });
});

describe('formatRelativeTime', () => {
  const now = new Date('2026-09-28T12:00:00Z');

  it('formats relative days, hours and at least one minute', () => {
    expect(formatRelativeTime('2026-09-25T11:00:00Z', now, 'en')).toBe('3 days ago');
    expect(formatRelativeTime('2026-09-28T07:30:00Z', now, 'en')).toBe('4 hours ago');
    expect(formatRelativeTime('2026-09-28T11:59:50Z', now, 'en')).toBe('1 minute ago');
  });

  it('formats relative time in Arabic with Latin digits', () => {
    expect(formatRelativeTime(new Date(now.getTime() - 2 * hourMs), now, 'ar')).toBe('قبل ساعتين');
    const tenDays = formatRelativeTime(new Date(now.getTime() - 240 * hourMs), now, 'ar');

    expect(tenDays).toContain('10');
    expect(tenDays).not.toMatch(arabicIndicDigit);
  });
});

describe('isRecent', () => {
  const now = new Date('2026-09-28T12:00:00Z');

  it('treats the last 24 hours as recent', () => {
    expect(isRecent(new Date(now.getTime() - 23 * hourMs), now)).toBe(true);
    expect(isRecent(new Date(now.getTime() - 25 * hourMs), now)).toBe(false);
    expect(isRecent(new Date(now.getTime() + hourMs), now)).toBe(false);
  });
});
