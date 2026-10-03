import { describe, expect, it } from 'vitest';
import { formatNumber, numberLocale } from './format';

const arabicIndicDigit = /[٠-٩۰-۹]/u;

describe('formatNumber', () => {
  it('formats Arabic numbers with Latin digits', () => {
    expect(formatNumber(1234, 'ar')).toBe('1,234');
  });

  it('formats English numbers with Latin digits', () => {
    expect(formatNumber(1234, 'en')).toBe('1,234');
  });

  it('passes Intl options through', () => {
    expect(formatNumber(0.5, 'en', { style: 'percent' })).toBe('50%');
  });

  it('picks the Latin Arabic locale and en-US otherwise', () => {
    expect(numberLocale('ar')).toBe('ar-EG-u-nu-latn');
    expect(numberLocale('en')).toBe('en-US');
    expect(numberLocale('fr')).toBe('en-US');
  });

  it('never prints Arabic-Indic digits in Arabic', () => {
    expect(formatNumber(9876543.21, 'ar')).not.toMatch(arabicIndicDigit);
  });
});
