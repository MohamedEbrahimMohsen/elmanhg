import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { useAssignTeacherSubject, useUnassignTeacherSubject } from '@/shared/api/generated/teachers/teachers';
import { getGetUsersQueryKey } from '@/shared/api/generated/users/users';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export interface TeacherSubjectsActions {
  toggle: (teacherId: string, subjectId: string, assign: boolean) => void;
  isPending: boolean;
}

export function useTeacherSubjects(): TeacherSubjectsActions {
  const { t } = useTranslation('users');
  const queryClient = useQueryClient();
  const options = {
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: [getGetUsersQueryKey()[0]] });
        toast.success(t('subjects.savedToast'));
      },
      onError: (error: unknown) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  };
  const assign = useAssignTeacherSubject(options);
  const unassign = useUnassignTeacherSubject(options);

  return {
    toggle: (teacherId, subjectId, shouldAssign) => {
      (shouldAssign ? assign : unassign).mutate({ teacherId, subjectId });
    },
    isPending: assign.isPending || unassign.isPending,
  };
}
