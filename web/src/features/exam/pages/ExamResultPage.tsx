import { Link, Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetExamAttempts, useGetExamSession } from '@/shared/api/generated/exams/exams';
import { Button } from '@/shared/ui/button';
import { examUnitNames, isMultiUnitExam, sortedExamItems, unitIdOf } from '../api/examSession';
import { ExamAttemptsSection } from '../components/ExamAttemptsSection';
import { ExamLessonBreakdown } from '../components/ExamLessonBreakdown';
import { ExamResultSummary } from '../components/ExamResultSummary';
import { ExamReviewItem } from '../components/ExamReviewItem';
import { ExamUnitBreakdown } from '../components/ExamUnitBreakdown';
import { ExamWeakestObjectives } from '../components/ExamWeakestObjectives';

export interface ExamResultPageProps {
  sessionId: string;
}

export function ExamResultPage({ sessionId }: ExamResultPageProps) {
  const { t } = useTranslation('exam');
  const { data, error, isPending, isError, refetch } = useGetExamSession(sessionId, { query: { staleTime: Infinity } });
  const attempts = useGetExamAttempts(sessionId);

  if (isError) {
    return (
      <ContentErrorState
        title={t('result.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('result.loading')} />;
  }
  if (data.submittedAt === null) {
    return <Navigate to="/student/exam/$sessionId" params={{ sessionId }} replace />;
  }

  const unitId = unitIdOf(data);
  const isMulti = isMultiUnitExam(data);
  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">
        {t(isMulti ? 'result.multiTitle' : 'result.title', { unit: examUnitNames(data) })}
      </h1>
      <ExamResultSummary session={data} />
      <ExamLessonBreakdown lessons={data.lessons} />
      {isMulti ? <ExamUnitBreakdown units={data.unitBreakdown} /> : null}
      <ExamWeakestObjectives objectives={data.weakestObjectives} />
      <ExamAttemptsSection query={attempts} currentSessionId={sessionId} />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.review')}</h2>
      {sortedExamItems(data).map((item) => (
        <ExamReviewItem key={item.questionId} item={item} sessionId={sessionId} />
      ))}
      <div className="flex flex-wrap gap-3">
        {isMulti && data.subjectId !== null ? (
          <Button asChild variant="primary">
            <Link
              to="/student/multi-exam"
              search={{
                subjectId: data.subjectId,
                unitIds: data.units.map((unit) => unit.unitId),
                size: data.items.length,
              }}
            >
              {t('result.retake')}
            </Link>
          </Button>
        ) : unitId !== null ? (
          <Button asChild variant="primary">
            <Link to="/student/exam-start/$unitId" params={{ unitId }}>
              {t('result.retake')}
            </Link>
          </Button>
        ) : null}
        <Button asChild variant="secondary">
          <Link to="/student/progress">{t('result.progress')}</Link>
        </Button>
      </div>
    </section>
  );
}
