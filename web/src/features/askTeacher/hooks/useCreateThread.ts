import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { CreateTeacherThreadBody } from '@/shared/api/generated/model';
import { getGetMyUsageQueryKey } from '@/shared/api/generated/subscriptions/subscriptions';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
  useCreateTeacherThread,
} from '@/shared/api/generated/teacher-threads/teacher-threads';
import type { AskTeacherFormValues } from '../schemas/askTeacherFormSchema';
import type { AskTeacherNewSearch } from '../schemas/askTeacherNewSearchSchema';

function toBody(context: AskTeacherNewSearch, values: AskTeacherFormValues): CreateTeacherThreadBody {
  const lessonId = context.lessonId ?? (values.lessonId || undefined);
  return {
    text: values.text,
    ...(lessonId === undefined ? {} : { lessonId }),
    ...(context.questionId === undefined ? {} : { questionId: context.questionId }),
    ...(context.attemptId === undefined ? {} : { attemptId: context.attemptId }),
    ...(values.image === null ? {} : { image: values.image }),
  };
}

export function useCreateThread(context: AskTeacherNewSearch) {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const mutation = useCreateTeacherThread({
    mutation: {
      onSuccess: async (result) => {
        queryClient.setQueryData(getGetMyTeacherThreadQueryKey(result.id), result);
        void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() });
        void queryClient.invalidateQueries({ queryKey: getGetMyUsageQueryKey() });
        toast.success(t('toast.sent'));
        await navigate({ to: '/student/thread/$threadId', params: { threadId: result.id } });
      },
    },
  });

  return {
    submit: (values: AskTeacherFormValues): Promise<unknown> => mutation.mutateAsync({ data: toBody(context, values) }),
  };
}
