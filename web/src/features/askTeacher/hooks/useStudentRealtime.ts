import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import { invalidateMastery } from '@/features/mastery';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
} from '@/shared/api/generated/teacher-threads/teacher-threads';
import { gradeReviewedSchema, teacherReplyReceivedSchema } from '@/shared/realtime/realtimeEvents';
import { useRealtimeEvents } from '@/shared/realtime/useRealtimeEvents';

const readsSession =
  (sessionId: string) =>
  ({ queryKey: [path] }: { queryKey: readonly unknown[] }) =>
    String(path).includes(sessionId);

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
      void queryClient.invalidateQueries({ predicate: readsSession(parsed.data.sessionId) });
      void invalidateMastery(queryClient);
      toast.info(t('realtime.gradeReviewed'));
    },
  });
}
