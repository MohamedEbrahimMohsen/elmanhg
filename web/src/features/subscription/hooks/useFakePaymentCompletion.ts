import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetMyPaymentQueryKey,
  getGetMyPaymentsQueryKey,
  useCompleteFakePayment,
} from '@/shared/api/generated/subscriptions/subscriptions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { invalidateEntitlementViews } from '../api/invalidateEntitlementViews';

export interface FakePaymentCompletion {
  complete: (succeeded: boolean) => void;
  isPending: boolean;
}

export function useFakePaymentCompletion(paymentId: string): FakePaymentCompletion {
  const { t } = useTranslation('subscription');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useCompleteFakePayment({
    mutation: {
      onSuccess: async (payment) => {
        queryClient.setQueryData(getGetMyPaymentQueryKey(paymentId), payment);
        await invalidateEntitlementViews(queryClient);
        await queryClient.invalidateQueries({ queryKey: [getGetMyPaymentsQueryKey()[0]] });
        await navigate({ to: '/student/checkout-result/$paymentId', params: { paymentId } });
      },
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return {
    complete: (succeeded) => {
      mutation.mutate({ paymentId, data: { succeeded } });
    },
    isPending: mutation.isPending,
  };
}
