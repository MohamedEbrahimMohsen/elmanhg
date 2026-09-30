import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { invalidateMastery } from '@/features/mastery';
import { getGetExamSessionQueryKey } from '@/shared/api/generated/exams/exams';
import {
  getGetEssayGradeQueryKey,
  getGetMathStepGradeQueryKey,
  getGetSessionQueryKey,
} from '@/shared/api/generated/sessions/sessions';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
} from '@/shared/api/generated/teacher-threads/teacher-threads';
import { gradeReviewedSchema, teacherReplyReceivedSchema } from '@/shared/realtime/realtimeEvents';
import { useRealtimeEvents } from '@/shared/realtime/useRealtimeEvents';

export function useStudentRealtime(): void {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();

  useRealtimeEvents({
    teacherReplyReceived: (payload) => {
      const parsed = teacherReplyReceivedSchema.safeParse(payload);
      if (!parsed.success) {
        return;
      }
      void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() });
      void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadQueryKey(parsed.data.threadId) });
      toast.info(t('realtime.newReply'));
    },
    gradeReviewed: (payload) => {
      const parsed = gradeReviewedSchema.safeParse(payload);
      if (!parsed.success) {
        return;
      }
      const { sessionId, questionId } = parsed.data;
      void queryClient.invalidateQueries({ queryKey: getGetEssayGradeQueryKey(sessionId, questionId) });
      void queryClient.invalidateQueries({ queryKey: getGetMathStepGradeQueryKey(sessionId, questionId) });
      void queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey(sessionId) });
      void queryClient.invalidateQueries({ queryKey: getGetExamSessionQueryKey(sessionId) });
      void invalidateMastery(queryClient);
      toast.info(t('realtime.gradeReviewed'));
    },
  });
}
