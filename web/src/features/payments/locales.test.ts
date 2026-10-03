import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerPaymentsLocales } from './locales';

describe('payments locales', () => {
  it('does not ship the payments namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'payments')).toBe(false);
  });

  it('resolves payments:page.title after registering', () => {
    registerPaymentsLocales();

    expect(i18n.t('payments:page.title', { lng: 'en' })).toBe('Payments');
    expect(i18n.t('payments:page.title', { lng: 'ar' })).toBe('المدفوعات');
  });
});
