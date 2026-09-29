import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
  useRateTeacherThread,
} from '@/shared/api/generated/teacher-threads/teacher-threads';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';

export function useRateThread(threadId: string) {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();
  const mutation = useRateTeacherThread({
    mutation: {
      onSuccess: (result) => {
        queryClient.setQueryData(getGetMyTeacherThreadQueryKey(threadId), result);
        void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() });
        toast.success(t('rating.sent'));
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        await queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadQueryKey(threadId) });
      },
    },
  });

  return {
    rate: (rating: number) => {
      mutation.mutate({ threadId, data: { rating } });
    },
    isPending: mutation.isPending,
  };
}
