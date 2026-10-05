import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { useFunnelEventOnMount } from '@/features/analytics';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { PublicPlanCards } from '@/features/subscription';
import { useGetPlanCatalogue } from '@/shared/api/generated/plans/plans';
import { useGetServableQuestionCount } from '@/shared/api/generated/questions/questions';
import { cn } from '@/shared/lib/utils';
import { layoutContainerClassName } from '@/shared/ui/layout';
import { LandingFooter } from '../components/LandingFooter';
import { LandingHero } from '../components/LandingHero';
import { LandingTopBar } from '../components/LandingTopBar';
import { ValueProps } from '../components/ValueProps';

export function LandingPage() {
  const { t } = useTranslation('landing');
  const plansHeadingId = useId();
  useFunnelEventOnMount('LandingViewed');
  const count = useGetServableQuestionCount();
  const catalogue = useGetPlanCatalogue();
  const plans = () => {
    if (catalogue.isError) {
      return (
        <ContentErrorState
          title={t('plans.errorTitle')}
          error={catalogue.error}
          onRetry={() => {
            void catalogue.refetch();
          }}
        />
      );
    }
    if (catalogue.isPending) {
      return <ContentListSkeleton label={t('plans.loading')} />;
    }
    return <PublicPlanCards catalogue={catalogue.data} />;
  };

  return (
    <>
      <LandingTopBar />
      <main id="main" className={cn(layoutContainerClassName, 'flex flex-col gap-10 pt-6 pb-10 lg:gap-16')}>
        <LandingHero servableCount={count.data ? Number(count.data.count) : undefined} />
        <ValueProps replySlaHours={catalogue.data ? Number(catalogue.data.askTeacher.replySlaHours) : undefined} />
        <section aria-labelledby={plansHeadingId} className="flex flex-col gap-3">
          <h2 id={plansHeadingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
            {t('plans.title')}
          </h2>
          {plans()}
        </section>
      </main>
      <LandingFooter />
    </>
  );
}
