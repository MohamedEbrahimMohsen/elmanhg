import { getI18n } from 'react-i18next';
import ar from './i18n/teacherStats/ar.json';
import en from './i18n/teacherStats/en.json';

export function registerTeacherStatsLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'teacherStats', ar, true, true);
  i18n.addResourceBundle('en', 'teacherStats', en, true, true);
}
