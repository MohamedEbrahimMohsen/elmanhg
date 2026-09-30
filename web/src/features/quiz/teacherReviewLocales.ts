import { getI18n } from 'react-i18next';
import ar from './i18n/teacherReview.ar.json';
import en from './i18n/teacherReview.en.json';

export const teacherReviewNamespace = 'quizTeacherReview';

export function registerTeacherReviewLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', teacherReviewNamespace, ar, true, true);
  i18n.addResourceBundle('en', teacherReviewNamespace, en, true, true);
}
