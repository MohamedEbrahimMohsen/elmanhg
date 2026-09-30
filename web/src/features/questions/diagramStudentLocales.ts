import { getI18n } from 'react-i18next';
import ar from './i18n/diagramStudent.ar.json';
import en from './i18n/diagramStudent.en.json';

export const diagramStudentNamespace = 'diagramStudent';

export function registerDiagramStudentLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', diagramStudentNamespace, ar, true, true);
  i18n.addResourceBundle('en', diagramStudentNamespace, en, true, true);
}
