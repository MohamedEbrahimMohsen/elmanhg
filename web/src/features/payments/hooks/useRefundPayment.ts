import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetPaymentLogQueryKey,
  getGetPaymentSettingsQueryKey,
  useRefundPayment as useRefundPaymentMutation,
} from '@/shared/api/generated/payments/payments';
import { ApiError } from '@/shared/lib/apiError';

const refundsDisabledCode = 'PAYMENT_REFUNDS_DISABLED';

export interface PaymentRefund {
  refund: (paymentId: string, reason: string, idempotencyKey: string) => Promise<unknown>;
  isPending: boolean;
}

export function useRefundPayment(): PaymentRefund {
  const { t } = useTranslation('payments');
  const queryClient = useQueryClient();
  const mutation = useRefundPaymentMutation({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: [getGetPaymentLogQueryKey()[0]] });
        toast.success(t('refund.done'));
      },
      onError: (error) => {
        if (error instanceof ApiError && error.code === refundsDisabledCode) {
          void queryClient.invalidateQueries({ queryKey: getGetPaymentSettingsQueryKey() });
        }
      },
    },
  });

  return {
    refund: (paymentId, reason, idempotencyKey) =>
      mutation.mutateAsync({ paymentId, data: { reason }, headers: { 'Idempotency-Key': idempotencyKey } }),
    isPending: mutation.isPending,
  };
}
