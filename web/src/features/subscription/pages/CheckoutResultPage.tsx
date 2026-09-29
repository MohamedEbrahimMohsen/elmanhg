import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { CheckoutStatusCard } from '../components/CheckoutStatusCard';
import { useCheckoutResult } from '../hooks/useCheckoutResult';

export interface CheckoutResultPageProps {
  paymentId: string;
}

export function CheckoutResultPage({ paymentId }: CheckoutResultPageProps) {
  const { t } = useTranslation('subscription');
  const { query, timedOut, checkAgain } = useCheckoutResult(paymentId);

  if (query.isError) {
    return (
      <ContentErrorState
        title={t('checkoutResult.errorTitle')}
        error={query.error}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  }
  if (query.isPending) {
    return <ContentListSkeleton label={t('checkoutResult.loading')} />;
  }
  return (
    <CheckoutStatusCard
      payment={query.data}
      timedOut={timedOut}
      isFetching={query.isFetching}
      onCheckAgain={checkAgain}
    />
  );
}
