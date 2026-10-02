import { getI18n } from 'react-i18next';
import errorsAr from './i18n/configurationErrors.ar.json';
import errorsEn from './i18n/configurationErrors.en.json';
import ar from './i18n/ar.json';
import en from './i18n/en.json';

export function registerConfigurationLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'configuration', ar, true, true);
  i18n.addResourceBundle('en', 'configuration', en, true, true);
  i18n.addResourceBundle('ar', 'common', errorsAr, true, false);
  i18n.addResourceBundle('en', 'common', errorsEn, true, false);
}
