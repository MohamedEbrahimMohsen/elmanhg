import { keepPreviousData } from '@tanstack/react-query';
import { useGetMyPayments } from '@/shared/api/generated/subscriptions/subscriptions';
import type { SubscriptionSearch } from '../schemas/subscriptionSearchSchema';

export const paymentHistoryPageSize = 10;

export function usePaymentHistory(search: SubscriptionSearch) {
  return useGetMyPayments(
    { pageNumber: search.paymentsPage ?? 1, pageSize: paymentHistoryPageSize },
    { query: { placeholderData: keepPreviousData } },
  );
}
