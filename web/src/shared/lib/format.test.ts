import { describe, expect, it } from 'vitest';
import { formatDate, formatMoney, formatNumber, numberLocale } from './format';

describe('formatNumber/formatDate', () => {
  it('formats Arabic numbers with Arabic-Indic digits by default', () => {
    expect(formatNumber(1234, 'ar')).toBe('١٬٢٣٤');
  });

  it('formats Arabic numbers with Latin digits when latin is requested', () => {
    expect(formatNumber(1234, 'ar', 'latin')).toBe('1,234');
  });

  it('formats English numbers with Latin digits', () => {
    expect(formatNumber(1234, 'en')).toBe('1,234');
  });

  it('formats Arabic dates with Arabic-Indic digits', () => {
    expect(
      formatDate(new Date(Date.UTC(2026, 8, 27)), 'ar', 'arabic-indic', { timeZone: 'UTC', year: 'numeric' }),
    ).toBe('٢٠٢٦');
  });

  it('uses en-US for an unknown language', () => {
    expect(numberLocale('fr', 'arabic-indic')).toBe('en-US');
  });
});

describe('formatMoney', () => {
  it('formats whole minor amounts without decimals in English', () => {
    expect(formatMoney(19900, 'EGP', 'en').replace(/\s/gu, ' ')).toBe('EGP 199');
  });

  it('keeps two decimals for fractional amounts', () => {
    expect(formatMoney(19950, 'EGP', 'en').replace(/\s/gu, ' ')).toBe('EGP 199.50');
  });

  it('uses Arabic-Indic digits for Arabic', () => {
    expect(formatMoney(19900, 'EGP', 'ar')).toContain('١٩٩');
  });
});
