import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerAuditLocales } from './locales';

describe('audit locales', () => {
  it('does not ship the audit namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'audit')).toBe(false);
  });

  it('resolves audit:page.title after registering', () => {
    registerAuditLocales();

    expect(i18n.t('audit:page.title', { lng: 'en' })).toBe('Audit log');
    expect(i18n.t('audit:page.title', { lng: 'ar' })).toBe('سجل التدقيق');
  });
});
