import { describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import quizEn from './i18n/en.json';
import { registerTeacherReviewLocales } from './teacherReviewLocales';

describe('registerTeacherReviewLocales', () => {
  it('keeps the teacher-review strings out of the eager quiz bundle', () => {
    expect(Object.keys(quizEn)).not.toContain('teacherReview');
  });

  it('registers the teacher-review strings in both languages', () => {
    registerTeacherReviewLocales();

    expect(i18n.t('quizTeacherReview:accepted', { lng: 'en' })).toBe(
      'Your teacher reviewed this grade and accepted it.',
    );
    expect(i18n.t('quizTeacherReview:accepted', { lng: 'ar' })).toBe('راجع معلمك هذا التصحيح واعتمده.');
  });
});
