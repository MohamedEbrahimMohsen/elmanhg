import { useId } from 'react';
import { keepPreviousData } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentSessionHistory } from '@/shared/api/generated/students/students';
import { Pagination } from '@/shared/components/Pagination';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { useStudentHistorySearch } from '../hooks/useStudentHistorySearch';
import { StudentHistoryTable } from './StudentHistoryTable';

const historyPageSize = 20;
const kinds = [
  { value: undefined, key: 'history.all' },
  { value: 'Quiz', key: 'history.quizzes' },
  { value: 'Exam', key: 'history.exams' },
] as const;

const pillClassName =
  'inline-flex min-h-11 items-center rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export interface StudentHistorySectionProps {
  studentId: string;
}

export function StudentHistorySection({ studentId }: StudentHistorySectionProps) {
  const { t } = useTranslation('users');
  const headingId = useId();
  const { search, setKind, setPage, clearFilter } = useStudentHistorySearch();
  const { data, error, isPending, isError, refetch } = useGetStudentSessionHistory(
    studentId,
    { pageNumber: search.page ?? 1, pageSize: historyPageSize, ...(search.kind ? { kind: search.kind } : {}) },
    { query: { placeholderData: keepPreviousData } },
  );
  const items = data?.items ?? [];
  const totalPages = Number(data?.totalPages ?? 0);

  const renderContent = () => {
    if (isPending) {
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
      return (
        <div className="flex flex-col items-start gap-3">
          <p className="text-ui text-text-muted">{t(search.kind ? 'history.noResults' : 'history.empty')}</p>
          {search.kind ? (
            <Button variant="secondary" onClick={clearFilter}>
              {t('history.clear')}
            </Button>
          ) : null}
        </div>
      );
    }
    return (
      <>
        <StudentHistoryTable items={items} />
        {totalPages > 1 ? (
          <Pagination page={Number(data.pageNumber ?? 1)} totalPages={totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('history.title')}
      </h2>
      <div role="group" aria-label={t('history.filterLabel')} className="flex flex-wrap gap-2">
        {kinds.map((kind) => (
          <button
            key={kind.key}
            type="button"
            aria-pressed={search.kind === kind.value}
            onClick={() => {
              setKind(kind.value);
            }}
            className={cn(
              pillClassName,
              search.kind === kind.value
                ? 'border-text bg-text text-surface'
                : 'border-border-strong bg-surface text-text hover:bg-soft',
            )}
          >
            {t(kind.key)}
          </button>
        ))}
      </div>
      {renderContent()}
    </section>
  );
}
