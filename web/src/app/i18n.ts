import i18next from 'i18next';
import ICU from 'i18next-icu';
import { initReactI18next } from 'react-i18next';
import { askTeacherLocales } from '@/features/askTeacher/locales';
import { avatarLocales } from '@/features/avatar/locales';
import { browseLocales } from '@/features/browse/locales';
import { contentLocales } from '@/features/content/locales';
import { examLocales } from '@/features/exam/locales';
import { landingLocales } from '@/features/landing/locales';
import { masteryLocales } from '@/features/mastery/locales';
import { mathStepsLocales } from '@/features/mathSteps/locales';
import { onboardingLocales } from '@/features/onboarding/locales';
import { progressLocales } from '@/features/progress/locales';
import { questionsLocales } from '@/features/questions/locales';
import { quizLocales } from '@/features/quiz/locales';
import { sessionLocales } from '@/features/session';
import { subscriptionLocales } from '@/features/subscription/locales';
import { shellLocales } from '@/features/shell';
import { numberLocale } from '@/shared/lib/format';
import commonAr from '@/shared/i18n/ar.json';

export const supportedLanguages = ['ar', 'en'] as const;

export type Language = (typeof supportedLanguages)[number];

export const defaultLanguage: Language = 'ar';

export const i18n = i18next.createInstance();

type BundleLoaders = Record<string, () => Promise<{ default: object }>>;

const arabicResources = {
  common: commonAr,
  session: sessionLocales.ar,
  shell: shellLocales.ar,
  content: contentLocales.ar,
  questions: questionsLocales.ar,
  quiz: quizLocales.ar,
  mastery: masteryLocales.ar,
  mathSteps: mathStepsLocales.ar,
  progress: progressLocales.ar,
  exam: examLocales.ar,
  subscription: subscriptionLocales.ar,
  browse: browseLocales.ar,
  landing: landingLocales.ar,
  onboarding: onboardingLocales.ar,
  avatar: avatarLocales.ar,
  askTeacher: askTeacherLocales.ar,
};

const lazyLanguageBundles: Partial<Record<Language, BundleLoaders>> = {
  en: {
    common: () => import('@/shared/i18n/en.json'),
    session: sessionLocales.en,
    shell: shellLocales.en,
    content: contentLocales.en,
    questions: questionsLocales.en,
    quiz: quizLocales.en,
    mastery: masteryLocales.en,
    mathSteps: mathStepsLocales.en,
    progress: progressLocales.en,
    exam: examLocales.en,
    subscription: subscriptionLocales.en,
    browse: browseLocales.en,
    landing: landingLocales.en,
    onboarding: onboardingLocales.en,
    avatar: avatarLocales.en,
    askTeacher: askTeacherLocales.en,
  },
};

export async function loadLanguage(
  lng: Language,
  loaders: BundleLoaders = lazyLanguageBundles[lng] ?? {},
): Promise<void> {
  const bundles = await Promise.all(
    Object.entries(loaders).map(async ([ns, load]) => [ns, (await load()).default] as const),
  );
  for (const [ns, bundle] of bundles) {
    i18n.addResourceBundle(lng, ns, bundle, true, true);
  }
}

export function applyDocumentLanguage(): void {
  const language = i18n.resolvedLanguage ?? defaultLanguage;
  document.documentElement.lang = language;
  document.documentElement.dir = i18n.dir(language);
}

export function initI18n(lng: Language = defaultLanguage): void {
  if (i18n.isInitialized) {
    void loadLanguage(lng)
      .then(() => i18n.changeLanguage(lng))
      .catch(() => undefined);
    return;
  }

  void i18n
    .use(new ICU({ parseLngForICU: numberLocale }))
    .use(initReactI18next)
    .init({
      lng,
      fallbackLng: defaultLanguage,
      supportedLngs: [...supportedLanguages],
      ns: Object.keys(arabicResources),
      defaultNS: 'common',
      resources: { ar: arabicResources },
      initAsync: false,
      interpolation: { escapeValue: false },
    });
  i18n.on('languageChanged', applyDocumentLanguage);
  applyDocumentLanguage();
}
