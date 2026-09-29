import { useTranslation } from 'react-i18next';
import { ContentEmptyState, ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Pagination } from '@/shared/components/Pagination';
import { useAskTeacherListSearch } from '../hooks/useAskTeacherListSearch';
import { useMyThreads } from '../hooks/useMyThreads';
import { ThreadListItem } from './ThreadListItem';

export function ThreadList() {
  const { t } = useTranslation('askTeacher');
  const { page, setPage } = useAskTeacherListSearch();
  const { data, error, isPending, isError, refetch } = useMyThreads(page);

  if (isPending) {
    return <ContentListSkeleton label={t('list.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('list.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  const items = data.items ?? [];
  if (items.length === 0) {
    return <ContentEmptyState message={t('list.empty')} />;
  }

  const totalPages = Number(data.totalPages);
  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-2">
        {items.map((thread) => (
          <ThreadListItem key={thread.id} thread={thread} />
        ))}
      </ul>
      {totalPages > 1 ? (
        <Pagination page={Number(data.pageNumber)} totalPages={totalPages} onPageChange={setPage} />
      ) : null}
    </div>
  );
}
