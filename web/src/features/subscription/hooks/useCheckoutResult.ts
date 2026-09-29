import { useState } from 'react';
import { useGetMyPayment } from '@/shared/api/generated/subscriptions/subscriptions';
import { checkoutConfirmationTimeoutMs, checkoutPollIntervalMs } from '../api/checkoutPolling';

export function useCheckoutResult(paymentId: string) {
  const [openedAt, setOpenedAt] = useState(() => Date.now());
  const query = useGetMyPayment(paymentId, {
    query: {
      refetchInterval: (current) =>
        current.state.data?.status === 'Pending' &&
        current.state.dataUpdatedAt - openedAt < checkoutConfirmationTimeoutMs
          ? checkoutPollIntervalMs
          : false,
    },
  });

  const checkAgain = () => {
    setOpenedAt(Date.now());
    void query.refetch();
  };

  return {
    query,
    checkAgain,
    timedOut: query.data?.status === 'Pending' && query.dataUpdatedAt - openedAt >= checkoutConfirmationTimeoutMs,
  };
}
