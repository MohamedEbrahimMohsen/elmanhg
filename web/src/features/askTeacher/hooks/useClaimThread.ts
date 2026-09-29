import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetInboxThreadQueryKey,
  getGetTeacherInboxQueryKey,
  useClaimTeacherThread,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export function useClaimThread(threadId: string) {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();
  const mutation = useClaimTeacherThread({
    mutation: {
      onSuccess: (result) => {
        queryClient.setQueryData(getGetInboxThreadQueryKey(threadId), result);
        void queryClient.invalidateQueries({ queryKey: getGetTeacherInboxQueryKey() });
        toast.success(t('inboxThread.claimed'));
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        await queryClient.invalidateQueries({ queryKey: getGetInboxThreadQueryKey(threadId) });
        void queryClient.invalidateQueries({ queryKey: getGetTeacherInboxQueryKey() });
      },
    },
  });

  return {
    claim: () => {
      mutation.mutate({ threadId });
    },
    isPending: mutation.isPending,
  };
}
