import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/ui/button';
import { hasActiveFilters } from '../api/questionListParams';
import { QuestionListEmptyState } from '../components/QuestionListEmptyState';
import { QuestionListFilters } from '../components/QuestionListFilters';
import { QuestionTable } from '../components/QuestionTable';
import { useQuestionList } from '../hooks/useQuestionList';
import { useQuestionListSearch } from '../hooks/useQuestionListSearch';

export function QuestionListPage() {
  const { t } = useTranslation('questions');
  const { search, applyFilters, setPage, clearFilters } = useQuestionListSearch();
  const { data, error, isPending, isError, refetch } = useQuestionList(search);

  const renderContent = () => {
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
    if (data.items.length === 0) {
      return (
        <QuestionListEmptyState variant={hasActiveFilters(search) ? 'no-results' : 'no-data'} onClear={clearFilters} />
      );
    }
    return (
      <>
        <QuestionTable items={data.items} />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('list.title')}</h1>
        {data ? <p className="text-caption text-text-muted">{t('list.count', { count: data.totalItems })}</p> : null}
        <p className="text-caption text-text-muted">{t('list.hint')}</p>
      </div>
      {search.lessonId ? (
        <div className="flex flex-wrap items-center gap-3">
          <p className="text-caption text-text">{t('list.lessonFilter')}</p>
          <Button asChild variant="secondary" size="sm">
            <Link to="/admin/question/new/$lessonId" params={{ lessonId: search.lessonId }}>
              {t('list.newInLesson')}
            </Link>
          </Button>
          <Button asChild variant="ghost" size="sm">
            <Link to="/admin/question/import/$lessonId" params={{ lessonId: search.lessonId }}>
              {t('list.importInLesson')}
            </Link>
          </Button>
        </div>
      ) : null}
      <QuestionListFilters
        key={JSON.stringify([
          search.status,
          search.type,
          search.subjectId,
          search.teacherId,
          search.minVersion,
          search.rejectionReason,
        ])}
        search={search}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
    </section>
  );
}
