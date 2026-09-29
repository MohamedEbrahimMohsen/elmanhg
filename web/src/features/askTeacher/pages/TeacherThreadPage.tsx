import { Link } from '@tanstack/react-router';
import { ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetMyTeacherThread } from '@/shared/api/generated/teacher-threads/teacher-threads';
import { ThreadContextCard } from '../components/ThreadContextCard';
import { ThreadMessage } from '../components/ThreadMessage';
import { useMarkThreadRead } from '../hooks/useMarkThreadRead';

export interface TeacherThreadPageProps {
  threadId: string;
}

export function TeacherThreadPage({ threadId }: TeacherThreadPageProps) {
  const { t } = useTranslation('askTeacher');
  const { data, error, isPending, isError, refetch } = useGetMyTeacherThread(threadId);
  useMarkThreadRead(threadId, data?.hasUnreadReply === true);

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
  return (
    <section className="flex flex-col gap-4">
      <nav aria-label={t('thread.breadcrumb')}>
        <ol className="flex flex-wrap items-center gap-2 text-caption text-text-muted">
          <li>
            <Link
              to="/student/ask"
              className="rounded-sm text-accent underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
            >
              {t('list.title')}
            </Link>
          </li>
          <li aria-hidden>
            <ChevronRight className="size-4 rtl:rotate-180" />
          </li>
          <li aria-current="page">{t('thread.title')}</li>
        </ol>
      </nav>
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('thread.title')}</h1>
      <ThreadContextCard thread={data} />
      <div className="flex flex-col gap-3">
        {data.messages.map((message) => (
          <ThreadMessage key={message.id} message={message} />
        ))}
      </div>
    </section>
  );
}
