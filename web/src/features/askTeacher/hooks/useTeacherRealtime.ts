import { useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import {
  getGetTeacherInboxQueryKey,
  getGetTeacherInboxRemindersQueryKey,
} from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { teacherThreadReminderSchema } from '@/shared/realtime/realtimeEvents';
import { useRealtimeEvents } from '@/shared/realtime/useRealtimeEvents';

export function useTeacherRealtime(): void {
  const { t } = useTranslation('askTeacher');
  const queryClient = useQueryClient();

  useRealtimeEvents({
    teacherThreadReminder: (payload) => {
      const parsed = teacherThreadReminderSchema.safeParse(payload);
      if (!parsed.success) {
        return;
      }
      void queryClient.invalidateQueries({ queryKey: getGetTeacherInboxRemindersQueryKey() });
      void queryClient.invalidateQueries({ queryKey: getGetTeacherInboxQueryKey() });
      toast.info(t('realtime.reminder'));
    },
  });
}
