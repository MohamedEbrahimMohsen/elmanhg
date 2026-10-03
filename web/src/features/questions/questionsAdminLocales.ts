import { getI18n } from 'react-i18next';
import ar from './i18n/admin.ar.json';
import en from './i18n/admin.en.json';

export function registerQuestionsAdminLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'questions', ar, true, true);
  i18n.addResourceBundle('en', 'questions', en, true, true);
}
