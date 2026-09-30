import { Link } from '@tanstack/react-router';
import { ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetInboxThread } from '@/shared/api/generated/teacher-inbox/teacher-inbox';
import { InboxThreadActions } from '../components/InboxThreadActions';
import { ThreadContextCard } from '../components/ThreadContextCard';
import { ThreadMessage } from '../components/ThreadMessage';
import { ThreadRating } from '../components/ThreadRating';

export interface InboxThreadPageProps {
  threadId: string;
}

export function InboxThreadPage({ threadId }: InboxThreadPageProps) {
  const { t } = useTranslation('askTeacher');
  const { data, error, isPending, isError, refetch } = useGetInboxThread(threadId);

  if (isPending) {
    return <ContentListSkeleton label={t('thread.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('thread.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  const teacherLabel = data.isClaimedByMe ? t('thread.you') : (data.teacherName ?? t('inbox.unclaimed'));
  const replyAuthor = data.isClaimedByMe ? t('thread.you') : (data.teacherName ?? t('thread.teacher'));
  return (
    <section className="flex flex-col gap-4">
      <nav aria-label={t('thread.breadcrumb')}>
        <ol className="flex flex-wrap items-center gap-2 text-caption text-text-muted">
          <li>
            <Link
              to="/teacher/inbox"
              className="rounded-sm text-accent underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
            >
              {t('inbox.title')}
            </Link>
          </li>
          <li aria-hidden>
            <ChevronRight className="size-4 rtl:rotate-180" />
          </li>
          <li aria-current="page">{t('thread.title')}</li>
        </ol>
      </nav>
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('thread.title')}</h1>
      <ThreadContextCard thread={data}>
        <p className="text-caption text-text-muted">
          {t('inboxThread.people', { student: data.studentName, teacher: teacherLabel })}
        </p>
      </ThreadContextCard>
      <div className="flex flex-col gap-3">
        {data.messages.map((message) => (
          <ThreadMessage
            key={message.id}
            message={message}
            authorLabel={message.isFromStudent ? data.studentName : replyAuthor}
          />
        ))}
      </div>
      {data.rating != null ? <ThreadRating rating={Number(data.rating)} /> : null}
      <InboxThreadActions thread={data} />
    </section>
  );
}
