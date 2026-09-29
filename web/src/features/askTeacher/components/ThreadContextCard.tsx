import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { TeacherThreadResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { ThreadStatusBadge } from './ThreadStatusBadge';

export interface ThreadContextCardProps {
  thread: TeacherThreadResult;
}

export function ThreadContextCard({ thread }: ThreadContextCardProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const { context } = thread;
  const due = formatDate(new Date(thread.slaDueAt), lng, 'arabic-indic', { dateStyle: 'medium', timeStyle: 'short' });

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
      <p className="text-ui font-semibold text-text">
        {t('thread.path', { subject: context.subjectName, lesson: context.lessonName })}
      </p>
      {context.questionStem ? (
        <div className="flex flex-col gap-1">
          <p className="text-caption font-semibold text-text">{t('context.question')}</p>
          <div className="text-ui text-text">
            <RichTextViewer html={context.questionStem} />
          </div>
        </div>
      ) : null}
      <div className="flex flex-wrap items-center gap-2">
        <p className="text-caption text-text-muted">{t('thread.due', { date: due })}</p>
        <ThreadStatusBadge thread={thread} />
      </div>
    </div>
  );
}
