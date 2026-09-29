import { useGetPaymentLog } from '@/shared/api/generated/payments/payments';

export function usePaymentReviewCount() {
  return useGetPaymentLog(
    { needsReview: true, pageNumber: 1, pageSize: 1 },
    { query: { select: (data) => Number(data.totalItems ?? 0) } },
  );
}
