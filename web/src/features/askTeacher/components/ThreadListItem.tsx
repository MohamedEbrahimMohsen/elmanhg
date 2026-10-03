import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { TeacherThreadSummaryResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { ThreadStatusBadge } from './ThreadStatusBadge';

export interface ThreadListItemProps {
  thread: TeacherThreadSummaryResult;
}

export function ThreadListItem({ thread }: ThreadListItemProps) {
  const { t, i18n } = useTranslation('askTeacher');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const date = formatDateTime(thread.submittedAt, lng);

  return (
    <li>
      <Link
        to="/student/thread/$threadId"
        params={{ threadId: thread.id }}
        className="flex items-start justify-between gap-3 rounded-md border border-border bg-surface px-3.5 py-3 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        <span className="flex min-w-0 flex-col gap-1">
          <span className="line-clamp-2 text-ui font-semibold text-text">{thread.questionText}</span>
          <span className="text-caption text-text-muted">
            {t('list.meta', { subject: thread.subjectName, lesson: thread.lessonName, date })}
          </span>
        </span>
        <span className="flex shrink-0 flex-col items-end gap-1">
          {thread.hasUnreadReply ? (
            <span className="inline-flex rounded-pill bg-accent px-2.5 py-0.5 text-micro font-semibold text-surface">
              {t('badge.newReply')}
            </span>
          ) : null}
          <ThreadStatusBadge thread={thread} />
        </span>
      </Link>
    </li>
  );
}
