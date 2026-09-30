import { getI18n } from 'react-i18next';
import ar from './i18n/diagram.ar.json';
import en from './i18n/diagram.en.json';
import errorsAr from './i18n/diagramErrors.ar.json';
import errorsEn from './i18n/diagramErrors.en.json';

export const diagramNamespace = 'questionsDiagram';

export function registerDiagramLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', diagramNamespace, ar, true, true);
  i18n.addResourceBundle('en', diagramNamespace, en, true, true);
  i18n.addResourceBundle('ar', 'common', errorsAr, true, false);
  i18n.addResourceBundle('en', 'common', errorsEn, true, false);
}
