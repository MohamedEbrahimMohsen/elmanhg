import { Mic, Square } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { formatDuration } from '../api/formatDuration';
import type { useVoiceRecorder } from '../hooks/useVoiceRecorder';

export interface VoiceRecorderControlsProps {
  recorder: ReturnType<typeof useVoiceRecorder>;
  maxSeconds: number;
  isUploading: boolean;
  onReRecord: () => void;
}

export function VoiceRecorderControls({ recorder, maxSeconds, isUploading, onReRecord }: VoiceRecorderControlsProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const max = formatDuration(maxSeconds, lng);

  if (recorder.status === 'unsupported') {
    return <p className="text-caption text-text-muted">{t('voice.unsupported')}</p>;
  }
  if (recorder.status === 'recording') {
    return (
      <div className="flex flex-wrap items-center gap-3">
        <p role="timer" aria-live="off" className="text-ui text-text-muted">
          {t('voice.recording', { elapsed: formatDuration(recorder.elapsedSeconds, lng), max })}
        </p>
        <Button variant="secondary" onClick={recorder.stop}>
          <Square aria-hidden className="size-4" />
          {t('voice.stop')}
        </Button>
      </div>
    );
  }
  if (recorder.status === 'recorded' && recorder.recording !== null) {
    return (
      <div className="flex flex-col gap-2">
        {/* eslint-disable-next-line jsx-a11y/media-has-caption -- a preview of the teacher's own recording; its transcript follows */}
        <audio controls src={recorder.recording.previewUrl} aria-label={t('voice.preview')} className="w-full" />
        <div>
          <Button variant="ghost" onClick={onReRecord} disabled={isUploading}>
            {t('voice.reRecord')}
          </Button>
        </div>
      </div>
    );
  }
  return (
    <div className="flex flex-col gap-2">
      {recorder.status === 'denied' ? <p className="text-caption text-text-muted">{t('voice.denied')}</p> : null}
      <div className="flex flex-wrap items-center gap-3">
        <Button
          variant="secondary"
          disabled={recorder.status === 'requesting'}
          onClick={() => {
            void recorder.start();
          }}
        >
          <Mic aria-hidden className="size-4" />
          {t('voice.record')}
        </Button>
        <p className="text-caption text-text-muted">{t('voice.limit', { max })}</p>
      </div>
    </div>
  );
}
