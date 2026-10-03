import { describe, expect, it } from 'vitest';
import { formatDuration } from './formatDuration';

describe('formatDuration', () => {
  it('formats seconds as m:ss in English', () => {
    expect(formatDuration(42, 'en')).toBe('0:42');
    expect(formatDuration(185, 'en')).toBe('3:05');
  });

  it('uses Latin digits in Arabic (digit policy)', () => {
    expect(formatDuration(42, 'ar')).toBe('0:42');
  });
});
