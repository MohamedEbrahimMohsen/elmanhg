import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetPaymentLogQueryKey,
  useResolvePaymentReview as useResolvePaymentReviewMutation,
} from '@/shared/api/generated/payments/payments';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface PaymentReviewResolution {
  resolve: (paymentId: string, onDone: () => void) => void;
  isPending: boolean;
}

export function useResolvePaymentReview(): PaymentReviewResolution {
  const { t } = useTranslation('payments');
  const queryClient = useQueryClient();
  const mutation = useResolvePaymentReviewMutation({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: [getGetPaymentLogQueryKey()[0]] });
        toast.success(t('resolve.done'));
      },
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return {
    resolve: (paymentId, onDone) => {
      mutation.mutate({ paymentId }, { onSettled: onDone });
    },
    isPending: mutation.isPending,
  };
}
