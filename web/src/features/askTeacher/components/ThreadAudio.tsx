import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { http } from '@/shared/lib/http';
import { blobToDataUrl } from '../api/blobToDataUrl';
import { formatDuration } from '../api/formatDuration';

export interface ThreadAudioProps {
  url: string;
  durationSeconds: number | null;
}

export function ThreadAudio({ url, durationSeconds }: ThreadAudioProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const audio = useQuery({
    queryKey: ['askTeacher', 'audio', url],
    queryFn: async ({ signal }) => blobToDataUrl(await http<Blob>(url, { signal }), 'audio/'),
    staleTime: Infinity,
  });

  if (audio.isPending) {
    return (
      <div role="status" aria-busy="true" aria-label={t('thread.audioLoading')} className="h-11 rounded-sm bg-soft" />
    );
  }
  if (audio.isError) {
    return <p className="text-caption text-danger">{t('thread.audioError')}</p>;
  }
  return (
    <div className="flex flex-col gap-1">
      {/* eslint-disable-next-line jsx-a11y/media-has-caption -- the transcript rendered beside the player is its text alternative */}
      <audio controls preload="metadata" src={audio.data} aria-label={t('thread.voiceLabel')} className="w-full" />
      {durationSeconds === null ? null : (
        <p className="text-caption text-text-muted">
          {t('thread.duration', { duration: formatDuration(durationSeconds, lng) })}
        </p>
      )}
    </div>
  );
}
