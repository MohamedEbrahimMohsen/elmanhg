import { useTranslation } from 'react-i18next';
import type { PlanCatalogueResult, PlanPriceResult } from '@/shared/api/generated/model';
import { formatMoney } from '@/shared/lib/format';

export interface PlanCardContent {
  title: string;
  priceLines: string[];
  features: string[];
}

export function usePlanCardContent(
  catalogue: PlanCatalogueResult,
): Record<'free' | 'base' | 'askTeacher', PlanCardContent> {
  const { t, i18n } = useTranslation('subscription');
  const { free, base, askTeacher } = catalogue;
  const priceLines = (prices: PlanPriceResult[]) =>
    prices.map((price) =>
      t(`price.${price.period}`, {
        price: formatMoney(Number(price.price.amountMinor), price.price.currency, i18n.language),
      }),
    );

  return {
    free: {
      title: t('plan.Free'),
      priceLines: [],
      features: [
        t('features.free.browse'),
        t('features.free.lessons', { count: Number(free.openLessonsPerUnit) }),
        t('features.free.quiz', { count: Number(free.dailyQuizQuestions) }),
        t('features.free.avatar', { count: Number(free.dailyAvatarMessages) }),
      ],
    },
    base: {
      title: t('plan.Base'),
      priceLines: priceLines(base.prices),
      features: [
        t('features.base.unlimited'),
        t('features.base.allLessons'),
        t('features.base.progress'),
        t('features.base.avatar', { count: Number(base.dailyAvatarMessages) }),
      ],
    },
    askTeacher: {
      title: t('plan.AskTeacher'),
      priceLines: priceLines(askTeacher.prices),
      features: [
        t('features.askTeacher.requiresBase'),
        t('features.askTeacher.quota', { count: Number(askTeacher.monthlyQuestions) }),
        t('features.askTeacher.sla', { hours: Number(askTeacher.replySlaHours) }),
      ],
    },
  };
}
