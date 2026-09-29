import { useGetTeacherVoiceDraft } from '@/shared/api/generated/teacher-inbox/teacher-inbox';

export const transcriptPollIntervalMs = 2000;

export function useVoiceDraft(threadId: string, draftId: string | null) {
  return useGetTeacherVoiceDraft(threadId, draftId ?? '', {
    query: {
      enabled: draftId !== null,
      refetchInterval: (query) => (query.state.data?.status === 'Pending' ? transcriptPollIntervalMs : false),
    },
  });
}
