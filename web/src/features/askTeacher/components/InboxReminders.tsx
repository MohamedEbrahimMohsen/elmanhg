import { useId } from 'react';
import { Link } from '@tanstack/react-router';
import { BellRing } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useGetTeacherInboxReminders } from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { ThreadStatusBadge } from './ThreadStatusBadge';

export function InboxReminders() {
  const { t } = useTranslation('askTeacher');
  const headingId = useId();
  const { data, isPending, isError } = useGetTeacherInboxReminders();

  if (isPending || isError || data.length === 0) {
    return null;
  }
  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1"
    >
      <div className="flex items-center gap-2">
        <BellRing className="size-5 text-warning" aria-hidden />
        <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
          {t('reminders.title')}
        </h2>
      </div>
      <p className="text-caption text-text-muted">{t('reminders.intro')}</p>
      <ul className="flex flex-col gap-2">
        {data.map((reminder) => (
          <li key={reminder.threadId}>
            <Link
              to="/teacher/thread/$threadId"
              params={{ threadId: reminder.threadId }}
              className="flex items-start justify-between gap-3 rounded-md border border-border bg-surface px-3.5 py-3 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
            >
              <span className="flex min-w-0 flex-col gap-1">
                <span className="line-clamp-2 text-ui font-bold text-text">{reminder.questionText}</span>
                <span className="text-caption text-text-muted">
                  {t('reminders.meta', {
                    subject: reminder.subjectName,
                    lesson: reminder.lessonName,
                    claim: reminder.isClaimedByMe ? t('inbox.claimedByMe') : t('inbox.unclaimed'),
                    kind: t(`reminders.kind.${reminder.kind}`),
                  })}
                </span>
              </span>
              <ThreadStatusBadge
                thread={{ status: 'Open', isOverdue: reminder.isOverdue, slaDueAt: reminder.slaDueAt }}
              />
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
