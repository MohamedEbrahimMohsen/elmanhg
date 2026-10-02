import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { InviteUserResult } from '@/shared/api/generated/model';
import { getGetUsersQueryKey, useInviteUser as useInviteUserMutation } from '@/shared/api/generated/users/users';

export interface InviteUserAction {
  invite: (values: {
    role: 'Teacher' | 'Admin';
    displayName: string;
    email: string;
    phoneNumber: string | null;
  }) => Promise<InviteUserResult>;
  isPending: boolean;
}

export function useInviteUser(): InviteUserAction {
  const { t } = useTranslation('users');
  const queryClient = useQueryClient();
  const mutation = useInviteUserMutation({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: [getGetUsersQueryKey()[0]] });
        toast.success(t('invite.createdToast'));
      },
    },
  });

  return {
    invite: (values) => mutation.mutateAsync({ data: values }),
    isPending: mutation.isPending,
  };
}
