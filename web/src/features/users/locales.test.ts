import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerUsersLocales } from './locales';

describe('users locales', () => {
  it('does not ship the users namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'users')).toBe(false);
  });

  it('resolves users:page.title after registering', () => {
    registerUsersLocales();

    expect(i18n.hasResourceBundle('en', 'users')).toBe(true);
    expect(i18n.t('users:page.title')).toBe('Users');
  });
});
