import { useState } from 'react';
import { ListChecks } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { TeacherStatsCard } from '@/features/dashboard';
import { useGetValidationQueueFilters } from '@/shared/api/generated/validation-queue/validation-queue';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/ui/button';
import { hasActiveValidationFilters } from '../api/validationQueueParams';
import { BulkApproveBar } from '../components/BulkApproveBar';
import { ValidationQueueFilters } from '../components/ValidationQueueFilters';
import { ValidationQueueList } from '../components/ValidationQueueList';
import { useBulkApprove } from '../hooks/useBulkApprove';
import { useReviewSession } from '../hooks/useReviewSession';
import { useValidationQueue } from '../hooks/useValidationQueue';
import { useValidationQueueSearch } from '../hooks/useValidationQueueSearch';

export function ValidationQueuePage() {
  const { t, i18n } = useTranslation('questions');
  const { search, applyFilters, setPage, clearFilters } = useValidationQueueSearch();
  const { reviewSessionId, query: session } = useReviewSession();
  const queue = useValidationQueue(search, reviewSessionId);
  const { data: filters } = useGetValidationQueueFilters();
  const bulk = useBulkApprove();
  const searchKey = JSON.stringify(search);
  const [selection, setSelection] = useState({ key: searchKey, ids: new Set<string>() });
  const selectedIds = selection.key === searchKey ? selection.ids : new Set<string>();
  const separator = i18n.resolvedLanguage === 'ar' ? '، ' : ', ';
  const subjects = (filters?.subjects ?? []).map((subject) => subject.name).join(separator);

  const toggle = (id: string) => {
    const ids = new Set(selectedIds);
    if (!ids.delete(id)) {
      ids.add(id);
    }
    setSelection({ key: searchKey, ids });
  };
  const approveSelected = async () => {
    if (reviewSessionId === undefined) {
      return;
    }
    await bulk.approve(reviewSessionId, [...selectedIds]);
    setSelection({ key: searchKey, ids: new Set<string>() });
  };

  const renderContent = () => {
    if (session.isError || queue.isError) {
      return (
        <ContentErrorState
          title={t('validation.queue.errorTitle')}
          error={session.isError ? session.error : queue.error}
          onRetry={() => {
            void (session.isError ? session.refetch() : queue.refetch());
          }}
        />
      );
    }
    if (session.isPending || queue.isPending) {
      return <ContentListSkeleton label={t('validation.queue.loading')} />;
    }
    const { items, pageNumber, totalPages } = queue.data;
    if (items.length === 0) {
      const noResults = hasActiveValidationFilters(search);
      return (
        <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
          <ListChecks aria-hidden className="size-8 text-text-muted" />
          <p className="text-ui text-text">
            {t(noResults ? 'validation.queue.empty.noResults' : 'validation.queue.empty.noData')}
          </p>
          {noResults ? (
            <Button variant="primary" onClick={clearFilters}>
              {t('validation.filters.clear')}
            </Button>
          ) : null}
        </div>
      );
    }
    return (
      <>
        {items.some((item) => item.openedInSession) ? (
          <BulkApproveBar count={selectedIds.size} isPending={bulk.isPending} onConfirm={approveSelected} />
        ) : null}
        <ValidationQueueList items={items} selectedIds={selectedIds} onToggle={toggle} />
        {totalPages > 1 ? <Pagination page={pageNumber} totalPages={totalPages} onPageChange={setPage} /> : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('validation.queue.title')}</h1>
        {subjects ? (
          <p className="text-caption text-text-muted">{t('validation.queue.subjects', { subjects })}</p>
        ) : null}
      </div>
      <TeacherStatsCard />
      <ValidationQueueFilters
        key={JSON.stringify([search.unitId, search.lessonId, search.type, search.difficulty, search.minAgeDays])}
        search={search}
        filters={filters}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
    </section>
  );
}
