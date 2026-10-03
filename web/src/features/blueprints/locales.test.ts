import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerBlueprintsLocales } from './locales';

describe('blueprints locales', () => {
  it('does not ship the blueprints namespace until registered', () => {
    expect(i18n.hasResourceBundle('en', 'blueprints')).toBe(false);
  });

  it('resolves blueprints:page.title after registering', () => {
    registerBlueprintsLocales();

    expect(i18n.t('blueprints:page.title', { lng: 'en' })).toBe('Exam blueprints');
    expect(i18n.t('blueprints:page.title', { lng: 'ar' })).toBe('نماذج الامتحانات');
  });
});
