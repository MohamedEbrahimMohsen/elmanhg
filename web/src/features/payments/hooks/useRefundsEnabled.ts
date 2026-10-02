import { useGetPaymentSettings } from '@/shared/api/generated/payments/payments';

export function useRefundsEnabled() {
  return useGetPaymentSettings({ query: { select: (data) => data.refundsEnabled } });
}
