import { getI18n } from 'react-i18next';
import ar from './i18n/ar.json';
import en from './i18n/en.json';

export function registerPaymentsLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'payments', ar, true, true);
  i18n.addResourceBundle('en', 'payments', en, true, true);
}
