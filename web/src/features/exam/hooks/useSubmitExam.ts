import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { getGetExamSessionQueryKey, useSubmitExam as useSubmitExamMutation } from '@/shared/api/generated/exams/exams';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { invalidateExamViews } from '../api/invalidateExamViews';

export interface SubmitExam {
  submit: () => void;
  isPending: boolean;
}

export function useSubmitExam(sessionId: string, flush: () => Promise<void>, onSubmitted?: () => void): SubmitExam {
  const { t } = useTranslation('exam');
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const submitting = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const key = getGetExamSessionQueryKey(sessionId);
  const mutation = useSubmitExamMutation({
    mutation: {
      onSuccess: async (session) => {
        onSubmitted?.();
        queryClient.setQueryData(key, session);
        void invalidateExamViews(queryClient);
        await navigate({ to: '/student/exam-result/$sessionId', params: { sessionId } });
      },
      onError: async (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
        submitting.current = false;
        setIsSubmitting(false);
        await queryClient.invalidateQueries({ queryKey: key });
      },
    },
  });

  return {
    submit: () => {
      if (submitting.current) {
        return;
      }
      submitting.current = true;
      setIsSubmitting(true);
      void flush().then(() => {
        mutation.mutate({ sessionId });
      });
    },
    isPending: isSubmitting || mutation.isPending,
  };
}
