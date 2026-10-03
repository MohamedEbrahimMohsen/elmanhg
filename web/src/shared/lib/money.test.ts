import { describe, expect, it } from 'vitest';
import { formatMoney } from './money';

describe('formatMoney', () => {
  it('formats whole minor amounts without decimals in English', () => {
    expect(formatMoney(19900, 'EGP', 'en').replace(/\s/gu, ' ')).toBe('EGP 199');
  });

  it('keeps two decimals for fractional amounts', () => {
    expect(formatMoney(19950, 'EGP', 'en').replace(/\s/gu, ' ')).toBe('EGP 199.50');
  });

  it('uses Latin digits in Arabic', () => {
    const text = formatMoney(19900, 'EGP', 'ar');

    expect(text).toContain('199');
    expect(text).not.toMatch(/[٠-٩۰-۹]/u);
  });
});
