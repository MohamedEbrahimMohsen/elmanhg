import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { useSetTeacherPhoneNumber } from '@/shared/api/generated/teachers/teachers';
import { getGetUsersQueryKey } from '@/shared/api/generated/users/users';

export interface TeacherPhoneNumberAction {
  save: (teacherId: string, phoneNumber: string | null) => Promise<void>;
  isPending: boolean;
}

export function useTeacherPhoneNumber(): TeacherPhoneNumberAction {
  const { t } = useTranslation('users');
  const queryClient = useQueryClient();
  const mutation = useSetTeacherPhoneNumber({
    mutation: {
      onSuccess: (_, { data }) => {
        void queryClient.invalidateQueries({ queryKey: [getGetUsersQueryKey()[0]] });
        toast.success(t(data.phoneNumber === null ? 'teacherPhone.removedToast' : 'teacherPhone.savedToast'));
      },
    },
  });

  return {
    save: (teacherId, phoneNumber) => mutation.mutateAsync({ teacherId, data: { phoneNumber } }),
    isPending: mutation.isPending,
  };
}
