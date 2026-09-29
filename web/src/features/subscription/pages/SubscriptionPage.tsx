import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetPlanCatalogue } from '@/shared/api/generated/plans/plans';
import { useGetMyEntitlement } from '@/shared/api/generated/subscriptions/subscriptions';
import { CurrentPlanCard } from '../components/CurrentPlanCard';
import { PaymentHistorySection } from '../components/PaymentHistorySection';
import { PlanCardGrid } from '../components/PlanCardGrid';
import { SubscribeHeader } from '../components/SubscribeHeader';

export function SubscriptionPage() {
  const { t } = useTranslation('subscription');
  const catalogue = useGetPlanCatalogue();
  const entitlement = useGetMyEntitlement();

  const renderPlans = () => {
    if (catalogue.isError || entitlement.isError) {
      return (
        <ContentErrorState
          title={t('page.errorTitle')}
          error={catalogue.error ?? entitlement.error}
          onRetry={() => {
            void catalogue.refetch();
            void entitlement.refetch();
          }}
        />
      );
    }
    if (catalogue.isPending || entitlement.isPending) {
      return <ContentListSkeleton label={t('page.loading')} />;
    }
    return (
      <>
        <CurrentPlanCard entitlement={entitlement.data} />
        <PlanCardGrid catalogue={catalogue.data} entitlement={entitlement.data} />
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <SubscribeHeader />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('page.title')}</h2>
      {renderPlans()}
      <PaymentHistorySection />
    </section>
  );
}
