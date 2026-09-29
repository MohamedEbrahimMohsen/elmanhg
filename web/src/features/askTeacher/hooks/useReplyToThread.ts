import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetInboxThreadQueryKey,
  getGetTeacherInboxQueryKey,
  useReplyToTeacherThread,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { ApiError } from '@/shared/lib/apiError';
import type { ReplyFormValues } from '../schemas/replyFormSchema';

export function useReplyToThread(threadId: string) {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();
  const mutation = useReplyToTeacherThread({
    mutation: {
      onSuccess: (result) => {
        queryClient.setQueryData(getGetInboxThreadQueryKey(threadId), result);
        void queryClient.invalidateQueries({ queryKey: getGetTeacherInboxQueryKey() });
        toast.success(t('inboxThread.sent'));
      },
      onError: async (error) => {
        if (error instanceof ApiError && error.status === 409) {
          await queryClient.invalidateQueries({ queryKey: getGetInboxThreadQueryKey(threadId) });
        }
      },
    },
  });

  return {
    submit: (values: ReplyFormValues): Promise<unknown> =>
      mutation.mutateAsync({ threadId, data: { text: values.text } }),
  };
}
