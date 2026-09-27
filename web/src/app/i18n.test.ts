import { describe, expect, it } from 'vitest';
import { i18n } from './i18n';

describe('i18n', () => {
  it('sets html lang ar and dir rtl for Arabic', async () => {
    await i18n.changeLanguage('ar');

    expect(document.documentElement).toHaveAttribute('lang', 'ar');
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('sets html lang en and dir ltr for English', async () => {
    await i18n.changeLanguage('en');

    expect(document.documentElement).toHaveAttribute('lang', 'en');
    expect(document.documentElement).toHaveAttribute('dir', 'ltr');
  });

  it('formats ICU numbers with Arabic-Indic digits in Arabic', () => {
    i18n.addResource('ar', 'common', 'test.count', '{count, plural, few {# أسئلة} other {# سؤال}}');

    expect(i18n.t('test.count', { lng: 'ar', count: 3 })).toBe('٣ أسئلة');
  });

  it('falls back to Arabic and rtl for an unsupported language', async () => {
    await i18n.changeLanguage('fr');

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(i18n.t('app.name')).toBe('المنهج');
  });
});
