import { Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMyPayment } from '@/shared/api/generated/subscriptions/subscriptions';
import { FakeCheckoutCard } from '../components/FakeCheckoutCard';
import { useFakePaymentCompletion } from '../hooks/useFakePaymentCompletion';

export interface FakeCheckoutPageProps {
  paymentId: string;
}

export function FakeCheckoutPage({ paymentId }: FakeCheckoutPageProps) {
  const { t } = useTranslation('subscription');
  const payment = useGetMyPayment(paymentId);
  const completion = useFakePaymentCompletion(paymentId);

  if (payment.isError) {
    return (
      <ContentErrorState
        title={t('fakeCheckout.errorTitle')}
        error={payment.error}
        onRetry={() => {
          void payment.refetch();
        }}
      />
    );
  }
  if (payment.isPending) {
    return <ContentListSkeleton label={t('fakeCheckout.loading')} />;
  }
  if (payment.data.status !== 'Pending') {
    return <Navigate to="/student/checkout-result/$paymentId" params={{ paymentId }} replace />;
  }
  return <FakeCheckoutCard payment={payment.data} isPending={completion.isPending} onComplete={completion.complete} />;
}
