import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetGradeReviewSubjects } from '@/shared/api/generated/grade-reviews/grade-reviews';
import { Pagination } from '@/shared/components/Pagination';
import { GradeReviewEmptyState } from '../components/GradeReviewEmptyState';
import { GradeReviewKindTabs } from '../components/GradeReviewKindTabs';
import { GradeReviewList } from '../components/GradeReviewList';
import { GradeReviewSubjectPicker } from '../components/GradeReviewSubjectPicker';
import { useGradeReviewQueue } from '../hooks/useGradeReviewQueue';
import { useGradeReviewSearch } from '../hooks/useGradeReviewSearch';

export function GradeReviewQueuePage() {
  const { t } = useTranslation('gradeReview');
  const { search, selectSubject, selectKind, setPage } = useGradeReviewSearch();
  const subjects = useGetGradeReviewSubjects();
  const subjectId = search.subjectId ?? subjects.data?.[0]?.subjectId;
  const queue = useGradeReviewQueue(subjectId, search.kind, search.page);
  const now = new Date();

  const renderQueue = (selectedId: string) => {
    if (queue.isError) {
      return (
        <ContentErrorState
          title={t('queue.errorTitle')}
          error={queue.error}
          onRetry={() => {
            void queue.refetch();
          }}
        />
      );
    }
    if (queue.isPending) {
      return <ContentListSkeleton label={t('queue.loading')} />;
    }
    const { pageNumber, totalPages } = queue.data;
    const items = queue.data.items ?? [];
    if (items.length === 0) {
      return <GradeReviewEmptyState messageKey="queue.empty.noItems" />;
    }
    return (
      <>
        <GradeReviewList items={items} subjectId={selectedId} now={now} />
        {Number(totalPages) > 1 ? (
          <Pagination page={Number(pageNumber)} totalPages={Number(totalPages)} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  const renderContent = () => {
    if (subjects.isError) {
      return (
        <ContentErrorState
          title={t('queue.errorTitle')}
          error={subjects.error}
          onRetry={() => {
            void subjects.refetch();
          }}
        />
      );
    }
    if (subjects.isPending) {
      return <ContentListSkeleton label={t('queue.loading')} />;
    }
    const selected = subjects.data.find((subject) => subject.subjectId === subjectId);
    if (subjectId === undefined || selected === undefined) {
      return <GradeReviewEmptyState messageKey="queue.empty.noSubjects" />;
    }
    return (
      <>
        <GradeReviewSubjectPicker subjects={subjects.data} selectedId={subjectId} onSelect={selectSubject} />
        <GradeReviewKindTabs
          kind={search.kind}
          essayCount={Number(selected.essayCount)}
          mathStepsCount={Number(selected.mathStepsCount)}
          onSelect={selectKind}
        />
        {renderQueue(subjectId)}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('queue.title')}</h1>
        <p className="text-caption text-text-muted">{t('queue.caption')}</p>
      </div>
      {renderContent()}
    </section>
  );
}
