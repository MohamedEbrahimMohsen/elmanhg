import { useTranslation } from 'react-i18next';
import { toast } from 'sonner';
import type { TeacherVoiceDraftResult } from '@/shared/api/generated/model';
import { useRecordTeacherVoiceDraft } from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { voiceFileName } from '../api/recorderMimeType';
import type { VoiceRecording } from './useVoiceRecorder';

export function useRecordVoiceDraft(threadId: string) {
  const { t } = useTranslation('askTeacher');
  const mutation = useRecordTeacherVoiceDraft({
    mutation: {
      onError: (error) => {
        const code = error instanceof ApiError ? error.code : unhandledErrorCode;
        toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']));
      },
    },
  });

  return {
    upload: (recording: VoiceRecording): Promise<TeacherVoiceDraftResult> =>
      mutation.mutateAsync({
        threadId,
        data: {
          audio: new File([recording.blob], voiceFileName(recording.mimeType), { type: recording.mimeType }),
          durationSeconds: recording.durationSeconds,
        },
      }),
    isPending: mutation.isPending,
  };
}
