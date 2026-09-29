import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetMyEntitlementQueryKey, useCancelSubscription } from '@/shared/api/generated/subscriptions/subscriptions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface SubscriptionCancellation {
  cancel: (subscriptionId: string, onDone: () => void) => void;
  isPending: boolean;
}

export function useSubscriptionCancellation(): SubscriptionCancellation {
  const { t } = useTranslation('subscription');
  const queryClient = useQueryClient();
  const mutation = useCancelSubscription({
    mutation: {
      onSuccess: (result) => {
        queryClient.setQueryData(getGetMyEntitlementQueryKey(), result);
        toast.success(t('cancel.done'));
      },
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return {
    cancel: (subscriptionId, onDone) => {
      mutation.mutate({ subscriptionId }, { onSettled: onDone });
    },
    isPending: mutation.isPending,
  };
}
