import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { AdminSubscriptionResult } from '@/shared/api/generated/model';
import {
  getGetStudentProfileQueryKey,
  useGrantComplimentarySubscription,
} from '@/shared/api/generated/students/students';
import { getGetUsersQueryKey } from '@/shared/api/generated/users/users';
import type { GrantPlanValues } from '../schemas/grantPlanSchema';

export interface GrantPlanAction {
  grant: (studentId: string, values: GrantPlanValues) => Promise<AdminSubscriptionResult>;
  isPending: boolean;
}

export function useGrantPlan(): GrantPlanAction {
  const { t } = useTranslation('users');
  const queryClient = useQueryClient();
  const mutation = useGrantComplimentarySubscription({
    mutation: {
      onSuccess: (_, { studentId }) => {
        void queryClient.invalidateQueries({ queryKey: getGetStudentProfileQueryKey(studentId) });
        void queryClient.invalidateQueries({ queryKey: [getGetUsersQueryKey()[0]] });
        toast.success(t('grant.doneToast'));
      },
    },
  });

  return {
    grant: (studentId, values) => mutation.mutateAsync({ studentId, data: values }),
    isPending: mutation.isPending,
  };
}
