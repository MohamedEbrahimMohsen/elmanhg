import { useEffect, useEffectEvent } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  getGetMyTeacherThreadQueryKey,
  getGetMyTeacherThreadsQueryKey,
  useMarkTeacherThreadRead,
} from '@/shared/api/generated/teacher-threads/teacher-threads';

export function useMarkThreadRead(threadId: string, hasUnreadReply: boolean): void {
  const queryClient = useQueryClient();
  const { mutate } = useMarkTeacherThreadRead({
    mutation: {
      onSuccess: () => {
        void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() });
        void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadQueryKey(threadId) });
      },
    },
  });
  const markRead = useEffectEvent((readThreadId: string) => {
    mutate({ threadId: readThreadId });
  });

  useEffect(() => {
    if (hasUnreadReply) {
      markRead(threadId);
    }
  }, [threadId, hasUnreadReply]);
}
