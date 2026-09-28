import { useEffect, useEffectEvent, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Pagination } from '@/shared/components/Pagination';
import { useProgressSearch } from '../hooks/useProgressSearch';
import { useSessionHistory } from '../hooks/useSessionHistory';
import { SessionHistoryEmptyState } from './SessionHistoryEmptyState';
import { SessionHistoryTable } from './SessionHistoryTable';
import { SessionKindFilter } from './SessionKindFilter';

export function SessionHistorySection() {
  const { t } = useTranslation('progress');
  const headingId = useId();
  const { search, setKind, setPage, clearFilter } = useProgressSearch();
  const { data, error, isPending, isError, refetch } = useSessionHistory(search);
  const items = data?.items ?? [];
  const totalPages = Number(data?.totalPages ?? 0);
  const pageNumber = Number(data?.pageNumber ?? 1);
  const pageOutOfRange = !isPending && !isError && items.length === 0 && totalPages > 0 && pageNumber > totalPages;
  const resetPage = useEffectEvent(() => {
    setPage(1);
  });

  useEffect(() => {
    if (pageOutOfRange) {
      resetPage();
    }
  }, [pageOutOfRange]);

  const renderContent = () => {
    if (isPending || pageOutOfRange) {
      return <ContentListSkeleton label={t('history.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('history.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (items.length === 0) {
      return <SessionHistoryEmptyState variant={search.kind ? 'no-results' : 'no-data'} onClear={clearFilter} />;
    }
    return (
      <>
        <SessionHistoryTable items={items} />
        {totalPages > 1 ? <Pagination page={pageNumber} totalPages={totalPages} onPageChange={setPage} /> : null}
      </>
    );
  };

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('history.title')}
      </h2>
      <SessionKindFilter value={search.kind} onChange={setKind} />
      {renderContent()}
    </section>
  );
}
