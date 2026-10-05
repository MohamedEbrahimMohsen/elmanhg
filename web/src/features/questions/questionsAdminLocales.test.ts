import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { registerQuestionsAdminLocales } from './questionsAdminLocales';

describe('registerQuestionsAdminLocales', () => {
  it('keeps the student question strings without the admin strings until registered', () => {
    expect(i18n.t('questions:types.Mcq', { lng: 'en' })).toBe('Multiple choice');
    expect(i18n.exists('questions:editor.newTitle', { lng: 'en' })).toBe(false);
  });

  it('adds the editor strings after registering', () => {
    registerQuestionsAdminLocales();

    expect(i18n.t('questions:editor.newTitle', { lng: 'en' })).toBe('New question');
    expect(i18n.t('questions:editor.newTitle', { lng: 'ar' })).toBe('سؤال جديد');
    expect(i18n.t('questions:view.true', { lng: 'en' })).toBe('True');
  });
});
