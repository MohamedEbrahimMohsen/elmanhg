import { getI18n } from 'react-i18next';
import ar from './i18n/assistant.ar.json';
import en from './i18n/assistant.en.json';

export function registerAssistantLocales(): void {
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'assistant', ar, true, true);
  i18n.addResourceBundle('en', 'assistant', en, true, true);
}
