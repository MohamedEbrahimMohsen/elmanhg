import { describe, expect, it, vi } from 'vitest';
import { i18n, initI18n, loadLanguage } from './i18n';

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

  it('formats ICU numbers with Latin digits in Arabic', () => {
    i18n.addResource('ar', 'common', 'test.count', '{count, plural, few {# أسئلة} other {# سؤال}}');

    expect(i18n.t('test.count', { lng: 'ar', count: 3 })).toBe('3 أسئلة');
  });

  it('falls back to Arabic and rtl for an unsupported language', async () => {
    await i18n.changeLanguage('fr');

    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
    expect(i18n.t('app.name')).toBe('المنهج');
  });

  it('loads the English bundles on demand', async () => {
    i18n.removeResourceBundle('en', 'quiz');
    expect(i18n.exists('quiz:practice.choose', { lng: 'en', fallbackLng: false })).toBe(false);

    await loadLanguage('en');

    expect(i18n.t('quiz:practice.choose', { lng: 'en' })).toBe('How many questions?');
    expect(i18n.t('app.name', { lng: 'en' })).toBe('Elmanhg');
    expect(i18n.t('questions:types.Mcq', { lng: 'en' })).toBe('Multiple choice');
  });

  it('adds no English bundle when one fails to load', async () => {
    await expect(
      loadLanguage('en', {
        probeLoaded: () => Promise.resolve({ default: { title: 'Loaded' } }),
        probeFailed: () => Promise.reject(new Error('offline')),
      }),
    ).rejects.toThrow('offline');

    expect(i18n.hasResourceBundle('en', 'probeLoaded')).toBe(false);
  });

  it('shows Arabic while an English bundle is missing', () => {
    i18n.addResourceBundle('ar', 'probeFallback', { title: 'عنوان' });

    expect(i18n.t('probeFallback:title', { lng: 'en' })).toBe('عنوان');
  });

  it('has no lazy bundles for Arabic', async () => {
    await expect(loadLanguage('ar')).resolves.toBeUndefined();

    expect(i18n.t('quiz:practice.choose', { lng: 'ar' })).toBe('كم سؤالًا تريد؟');
  });

  it('loads English before an initialised instance switches to it', async () => {
    await i18n.changeLanguage('ar');
    i18n.removeResourceBundle('en', 'quiz');
    const textOnSwitch = new Promise<string>((resolve) => {
      const onChange = () => {
        i18n.off('languageChanged', onChange);
        resolve(i18n.t('quiz:practice.choose'));
      };
      i18n.on('languageChanged', onChange);
    });

    initI18n('en');

    await expect(textOnSwitch).resolves.toBe('How many questions?');
    expect(document.documentElement).toHaveAttribute('lang', 'en');
  });

  it('keeps the current language when a re-init load fails', async () => {
    await i18n.changeLanguage('ar');
    const addBundle = vi.spyOn(i18n, 'addResourceBundle').mockImplementationOnce(() => {
      throw new Error('offline');
    });
    const changeLanguage = vi.spyOn(i18n, 'changeLanguage');

    initI18n('en');
    await vi.waitFor(() => {
      expect(addBundle).toHaveBeenCalled();
    });
    await new Promise((resolve) => setTimeout(resolve, 0));
    const switches = changeLanguage.mock.calls.length;
    addBundle.mockRestore();
    changeLanguage.mockRestore();

    expect(switches).toBe(0);
    expect(i18n.language).toBe('ar');
    expect(document.documentElement).toHaveAttribute('lang', 'ar');
  });
});
