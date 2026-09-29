import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { TeacherInboxItemResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { ThreadStatusBadge } from './ThreadStatusBadge';

export interface InboxListItemProps {
  thread: TeacherInboxItemResult;
}

export function InboxListItem({ thread }: InboxListItemProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = formatDate(new Date(thread.submittedAt), lng, 'arabic-indic', {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
  const claim = thread.isClaimedByMe
    ? t('inbox.claimedByMe')
    : thread.teacherName
      ? t('inbox.claimedBy', { teacher: thread.teacherName })
      : t('inbox.unclaimed');

  return (
    <li>
      <Link
        to="/teacher/thread/$threadId"
        params={{ threadId: thread.id }}
        className="flex items-start justify-between gap-3 rounded-md border border-border bg-surface px-3.5 py-3 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <span className="flex min-w-0 flex-col gap-1">
          <span className="line-clamp-2 text-ui font-semibold text-text">{thread.questionText}</span>
          <span className="text-caption text-text-muted">
            {t('inbox.meta', {
              subject: thread.subjectName,
              lesson: thread.lessonName,
              student: thread.studentName,
              date,
            })}
          </span>
          <span className="text-caption text-text-muted">{claim}</span>
        </span>
        <ThreadStatusBadge thread={thread} />
      </Link>
    </li>
  );
}
