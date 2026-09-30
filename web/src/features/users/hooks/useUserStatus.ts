import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetStudentProfileQueryKey } from '@/shared/api/generated/students/students';
import { getGetUsersQueryKey, useReactivateUser, useSuspendUser } from '@/shared/api/generated/users/users';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface UserStatusActions {
  suspend: (userId: string) => Promise<void>;
  reactivate: (userId: string) => Promise<void>;
  isPending: boolean;
}

export function useUserStatus(): UserStatusActions {
  const { t } = useTranslation('users');
  const queryClient = useQueryClient();
  const refresh = (userId: string) => {
    void queryClient.invalidateQueries({ queryKey: [getGetUsersQueryKey()[0]] });
    void queryClient.invalidateQueries({ queryKey: getGetStudentProfileQueryKey(userId) });
  };
  const showError = (error: unknown) => {
    const code = error instanceof ApiError ? error.code : unhandledErrorCode;
    toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
  };
  const suspend = useSuspendUser({
    mutation: {
      onSuccess: (_, { userId }) => {
        refresh(userId);
        toast.success(t('status.suspendedToast'));
      },
      onError: showError,
    },
  });
  const reactivate = useReactivateUser({
    mutation: {
      onSuccess: (_, { userId }) => {
        refresh(userId);
        toast.success(t('status.reactivatedToast'));
      },
      onError: showError,
    },
  });

  return {
    suspend: (userId) => suspend.mutateAsync({ userId }),
    reactivate: (userId) => reactivate.mutateAsync({ userId }),
    isPending: suspend.isPending || reactivate.isPending,
  };
}
