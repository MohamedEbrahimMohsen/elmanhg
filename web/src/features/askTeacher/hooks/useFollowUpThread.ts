import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
  useFollowUpTeacherThread,
} from '@/shared/api/generated/teacher-threads/teacher-threads';
import { ApiError } from '@/shared/lib/apiError';
import type { FollowUpFormValues } from '../schemas/followUpFormSchema';

export function useFollowUpThread(threadId: string) {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();
  const mutation = useFollowUpTeacherThread({
    mutation: {
      onSuccess: (result) => {
        queryClient.setQueryData(getGetMyTeacherThreadQueryKey(threadId), result);
        void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() });
        toast.success(t('followUp.sent'));
      },
      onError: async (error) => {
        if (error instanceof ApiError && error.status === 409) {
          await queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadQueryKey(threadId) });
        }
      },
    },
  });

  return {
    submit: (values: FollowUpFormValues): Promise<unknown> =>
      mutation.mutateAsync({ threadId, data: { text: values.text } }),
  };
}
