import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetVoiceReplySettings } from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { useRecordVoiceDraft } from '../hooks/useRecordVoiceDraft';
import { useVoiceDraft } from '../hooks/useVoiceDraft';
import { useVoiceRecorder } from '../hooks/useVoiceRecorder';
import { VoiceRecorderControls } from './VoiceRecorderControls';
import { VoiceTranscriptForm } from './VoiceTranscriptForm';

export interface VoiceReplyPanelProps {
  threadId: string;
}

export function VoiceReplyPanel({ threadId }: VoiceReplyPanelProps) {
  const { t } = useTranslation('askTeacher');
  const settings = useGetVoiceReplySettings({ query: { staleTime: Infinity } });
  const maxSeconds = settings.data === undefined ? 0 : Number(settings.data.maxDurationSeconds);
  const [draftId, setDraftId] = useState<string | null>(null);
  const { upload, isPending: isUploading } = useRecordVoiceDraft(threadId);
  const recorder = useVoiceRecorder({
    maxSeconds,
    onRecorded: (recording) => {
      void upload(recording).then(
        (draft) => {
          setDraftId(draft.id);
        },
        () => undefined,
      );
    },
  });
  const draft = useVoiceDraft(threadId, draftId);

  if (settings.isPending) {
    return <ContentListSkeleton label={t('thread.loading')} />;
  }
  if (settings.isError) {
    return (
      <ContentErrorState
        title={t('thread.errorTitle')}
        error={settings.error}
        onRetry={() => {
          void settings.refetch();
        }}
      />
    );
  }
  const current = draftId === null ? undefined : draft.data;
  return (
    <div className="flex flex-col gap-4">
      <VoiceRecorderControls
        recorder={recorder}
        maxSeconds={maxSeconds}
        isUploading={isUploading}
        onReRecord={() => {
          recorder.reset();
          setDraftId(null);
        }}
      />
      {isUploading ? <p role="status">{t('voice.uploading')}</p> : null}
      {current?.status === 'Pending' ? (
        <p role="status" aria-live="polite">
          {t('voice.transcribing')}
        </p>
      ) : null}
      {current?.status === 'Failed' ? (
        <p className="text-caption text-text-muted">{t('voice.transcriptionFailed')}</p>
      ) : null}
      {current?.status === 'Ready' || current?.status === 'Failed' ? (
        <VoiceTranscriptForm
          key={current.id}
          threadId={threadId}
          draftId={current.id}
          defaultText={current.transcript ?? ''}
        />
      ) : null}
    </div>
  );
}
