import { keepPreviousData } from '@tanstack/react-query';
import { useGetPaymentLog } from '@/shared/api/generated/payments/payments';
import { toPaymentLogPage, toPaymentLogParams } from '../api/paymentLogParams';
import type { PaymentLogSearch } from '../schemas/paymentLogSearchSchema';

export function usePaymentLog(search: PaymentLogSearch) {
  return useGetPaymentLog(toPaymentLogParams(search), {
    query: { placeholderData: keepPreviousData, select: toPaymentLogPage },
  });
}
