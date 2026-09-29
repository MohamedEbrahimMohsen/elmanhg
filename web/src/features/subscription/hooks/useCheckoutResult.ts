import { useState } from 'react';
import { useGetMyPayment } from '@/shared/api/generated/subscriptions/subscriptions';
import { checkoutConfirmationTimeoutMs, checkoutPollIntervalMs } from '../api/checkoutPolling';

export function useCheckoutResult(paymentId: string) {
  const [openedAt] = useState(() => Date.now());
  const query = useGetMyPayment(paymentId, {
    query: {
      refetchInterval: (current) =>
        current.state.data?.status === 'Pending' &&
        current.state.dataUpdatedAt - openedAt < checkoutConfirmationTimeoutMs
          ? checkoutPollIntervalMs
          : false,
    },
  });

  return {
    query,
    timedOut: query.data?.status === 'Pending' && query.dataUpdatedAt - openedAt >= checkoutConfirmationTimeoutMs,
  };
}
