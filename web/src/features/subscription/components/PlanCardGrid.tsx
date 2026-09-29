import { useTranslation } from 'react-i18next';
import type { EntitlementResult, PlanCatalogueResult, PlanPriceResult } from '@/shared/api/generated/model';
import { formatMoney } from '@/shared/lib/format';
import { PlanCard } from './PlanCard';

export interface PlanCardGridProps {
  catalogue: PlanCatalogueResult;
  entitlement: EntitlementResult;
}

export function PlanCardGrid({ catalogue, entitlement }: PlanCardGridProps) {
  const { t, i18n } = useTranslation('subscription');
  const { free, base, askTeacher } = catalogue;
  const priceLines = (prices: PlanPriceResult[]) =>
    prices.map((price) =>
      t(`price.${price.period}`, {
        price: formatMoney(Number(price.price.amountMinor), price.price.currency, i18n.language),
      }),
    );

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      <PlanCard
        title={t('plan.Free')}
        priceLines={[]}
        features={[
          t('features.free.browse'),
          t('features.free.lessons', { count: Number(free.openLessonsPerUnit) }),
          t('features.free.quiz', { count: Number(free.dailyQuizQuestions) }),
          t('features.free.avatar', { count: Number(free.dailyAvatarMessages) }),
        ]}
        isActive={false}
      />
      <PlanCard
        title={t('plan.Base')}
        priceLines={priceLines(base.prices)}
        features={[
          t('features.base.unlimited'),
          t('features.base.allLessons'),
          t('features.base.progress'),
          t('features.base.avatar', { count: Number(base.dailyAvatarMessages) }),
        ]}
        isActive={entitlement.tier === 'Base'}
      />
      <PlanCard
        title={t('plan.AskTeacher')}
        priceLines={priceLines(askTeacher.prices)}
        features={[
          t('features.askTeacher.requiresBase'),
          t('features.askTeacher.quota', { count: Number(askTeacher.monthlyQuestions) }),
          t('features.askTeacher.sla', { hours: Number(askTeacher.replySlaHours) }),
        ]}
        isActive={entitlement.hasAskTeacher}
      />
    </div>
  );
}
