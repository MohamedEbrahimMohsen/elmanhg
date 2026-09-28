import i18next from 'i18next';
import ICU from 'i18next-icu';
import { initReactI18next } from 'react-i18next';
import { auditLocales } from '@/features/audit';
import { contentLocales } from '@/features/content/locales';
import { masteryLocales } from '@/features/mastery/locales';
import { questionsLocales } from '@/features/questions/locales';
import { quizLocales } from '@/features/quiz/locales';
import { sessionLocales } from '@/features/session';
import { shellLocales } from '@/features/shell';
import { numberLocale } from '@/shared/lib/format';
import commonAr from '@/shared/i18n/ar.json';
import commonEn from '@/shared/i18n/en.json';

export const supportedLanguages = ['ar', 'en'] as const;

export type Language = (typeof supportedLanguages)[number];

export const defaultLanguage: Language = 'ar';

export const i18n = i18next.createInstance();

const resources = {
  ar: {
    common: commonAr,
    session: sessionLocales.ar,
    shell: shellLocales.ar,
    audit: auditLocales.ar,
    content: contentLocales.ar,
    questions: questionsLocales.ar,
    quiz: quizLocales.ar,
    mastery: masteryLocales.ar,
  },
  en: {
    common: commonEn,
    session: sessionLocales.en,
    shell: shellLocales.en,
    audit: auditLocales.en,
    content: contentLocales.en,
    questions: questionsLocales.en,
    quiz: quizLocales.en,
    mastery: masteryLocales.en,
  },
};

export function applyDocumentLanguage(): void {
  const language = i18n.resolvedLanguage ?? defaultLanguage;
  document.documentElement.lang = language;
  document.documentElement.dir = i18n.dir(language);
}

export function initI18n(lng: Language = defaultLanguage): void {
  if (i18n.isInitialized) {
    void i18n.changeLanguage(lng);
    return;
  }

  void i18n
    .use(new ICU({ parseLngForICU: (code) => numberLocale(code, 'arabic-indic') }))
    .use(initReactI18next)
    .init({
      lng,
      fallbackLng: defaultLanguage,
      supportedLngs: [...supportedLanguages],
      ns: ['common', 'session', 'shell', 'audit', 'content', 'questions', 'quiz', 'mastery'],
      defaultNS: 'common',
      resources,
      initAsync: false,
      interpolation: { escapeValue: false },
    });
  i18n.on('languageChanged', applyDocumentLanguage);
  applyDocumentLanguage();
}
