import { useTranslation } from 'react-i18next';
import { ContentEmptyState, ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Pagination } from '@/shared/components/Pagination';
import { useTeacherInbox } from '../hooks/useTeacherInbox';
import { useTeacherInboxSearch } from '../hooks/useTeacherInboxSearch';
import { InboxListItem } from './InboxListItem';

export function InboxList() {
  const { t } = useTranslation('askTeacher');
  const { filter, page, setPage } = useTeacherInboxSearch();
  const { data, error, isPending, isError, refetch } = useTeacherInbox(filter, page);

  if (isPending) {
    return <ContentListSkeleton label={t('inbox.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('inbox.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  const items = data.items ?? [];
  if (items.length === 0) {
    return <ContentEmptyState message={t('inbox.empty')} />;
  }

  const totalPages = Number(data.totalPages);
  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-2">
        {items.map((thread) => (
          <InboxListItem key={thread.id} thread={thread} />
        ))}
      </ul>
      {totalPages > 1 ? (
        <Pagination page={Number(data.pageNumber)} totalPages={totalPages} onPageChange={setPage} />
      ) : null}
    </div>
  );
}
