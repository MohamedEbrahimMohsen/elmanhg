import { useRouter } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { BillingPeriod, SubscriptionPlan } from '@/shared/api/generated/model';
import { useStartCheckout } from '@/shared/api/generated/subscriptions/subscriptions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { isInAppUrl, redirectToExternal } from '@/shared/lib/redirect';

export interface Checkout {
  start: (plan: SubscriptionPlan, period: BillingPeriod) => void;
  isPending: boolean;
}

export function useCheckout(): Checkout {
  const { t } = useTranslation('subscription');
  const router = useRouter();
  const mutation = useStartCheckout({
    mutation: {
      onSuccess: (result) => {
        if (isInAppUrl(result.redirectUrl)) {
          router.history.push(result.redirectUrl);
        } else {
          redirectToExternal(result.redirectUrl);
        }
      },
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return {
    start: (plan, period) => {
      mutation.mutate({ data: { plan, period } });
    },
    isPending: mutation.isPending,
  };
}
