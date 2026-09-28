import { describe, expect, it } from 'vitest';
import { formatPendingAge } from './pendingAge';

const now = new Date('2026-09-28T12:00:00Z');

describe('formatPendingAge', () => {
  it('formats days in English', () => {
    expect(formatPendingAge('2026-09-25T11:00:00Z', now, 'en')).toBe('3 days ago');
  });

  it('formats hours when under a day', () => {
    expect(formatPendingAge('2026-09-28T07:30:00Z', now, 'en')).toBe('4 hours ago');
  });

  it('formats at least one minute', () => {
    expect(formatPendingAge('2026-09-28T11:59:50Z', now, 'en')).toBe('1 minute ago');
  });

  it('formats days in Arabic with Latin digits', () => {
    const text = formatPendingAge('2026-09-18T12:00:00Z', now, 'ar');

    expect(text).toContain('10');
    expect(text).not.toMatch(/[٠-٩]/);
  });
});
