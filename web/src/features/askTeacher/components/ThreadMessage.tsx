import { useTranslation } from 'react-i18next';
import type { TeacherMessageResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { ThreadImage } from './ThreadImage';

export interface ThreadMessageProps {
  message: TeacherMessageResult;
}

export function ThreadMessage({ message }: ThreadMessageProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = formatDate(new Date(message.createdAt), lng, 'arabic-indic', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <article
      className={cn(
        'flex flex-col gap-2 rounded-md p-3',
        message.isFromStudent ? 'bg-soft' : 'border border-border bg-surface',
      )}
    >
      <p className="text-caption text-text-muted">
        {t(message.isFromStudent ? 'thread.you' : 'thread.teacher')} · {date}
      </p>
      <p className="text-ui whitespace-pre-wrap text-text">{message.text}</p>
      {message.imageUrl ? <ThreadImage url={message.imageUrl} /> : null}
    </article>
  );
}
