import { describe, expect, it } from 'vitest';
import {
  formatAmount,
  formatCompact,
  formatCount,
  formatDay,
  formatElapsed,
  formatRate,
  formatRatio,
} from './metricFormat';

describe('metricFormat', () => {
  it('formats counts from numbers and strings', () => {
    expect(formatCount(1250, 'en')).toBe('1,250');
    expect(formatCount('1250', 'en')).toBe('1,250');
  });

  it('formats Arabic counts with ASCII digits', () => {
    const formatted = formatCount(1250, 'ar');

    expect(formatted).toBe('1,250');
    expect(formatted).not.toMatch(/[٠-٩]/u);
  });

  it('formats rates as percents and null as a dash', () => {
    expect(formatRate(0.925, 'en')).toBe('92.5%');
    expect(formatRate('0.75', 'en')).toBe('75%');
    expect(formatRate(null, 'en')).toBe('—');
    expect(formatRate(0.75, 'ar')).toContain('75');
    expect(formatRate(0.75, 'ar')).not.toMatch(/[٠-٩]/u);
  });

  it('formats ratios with up to two decimals', () => {
    expect(formatRatio(3.25, 'en')).toBe('3.25');
    expect(formatRatio(null, 'en')).toBe('—');
    expect(formatRatio(3.25, 'ar')).toBe('3.25');
  });

  it('picks the duration unit by size', () => {
    expect(formatElapsed(45, 'en')).toBe('45 seconds');
    expect(formatElapsed(150, 'en')).toBe('3 minutes');
    expect(formatElapsed(5400, 'en')).toBe('1.5 hours');
    expect(formatElapsed(129600, 'en')).toBe('1.5 days');
    expect(formatElapsed(null, 'en')).toBe('—');
  });

  it('formats Arabic durations with ASCII digits', () => {
    const formatted = formatElapsed(150, 'ar');

    expect(formatted).toContain('3');
    expect(formatted).toMatch(/دقائق/u);
  });

  it('formats money from minor units', () => {
    expect(formatAmount({ amountMinor: '19900', currency: 'EGP' }, 'en')).toMatch(/EGP\s199/u);
    const arabic = formatAmount({ amountMinor: 895500, currency: 'EGP' }, 'ar');
    expect(arabic).toContain('8,955');
    expect(arabic).not.toMatch(/[٠-٩]/u);
  });

  it('formats a day as short month and day', () => {
    expect(formatDay('2026-09-30', 'en')).toBe('Sep 30');
    const arabic = formatDay('2026-09-30', 'ar');
    expect(arabic).toContain('30');
    expect(arabic).toContain('سبتمبر');
  });

  it('formats compact axis values', () => {
    expect(formatCompact(2800, 'en')).toBe('2.8K');
    const arabic = formatCompact(2800, 'ar');
    expect(arabic).toContain('2.8');
    expect(arabic).not.toMatch(/[٠-٩]/u);
  });
});
