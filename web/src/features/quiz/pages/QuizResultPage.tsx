import { Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSession } from '@/shared/api/generated/sessions/sessions';
import { lessonIdOf, sortedItems } from '../api/quizSession';
import { NewPracticeButton } from '../components/NewPracticeButton';
import { QuizResultSummary } from '../components/QuizResultSummary';
import { QuizReviewItem } from '../components/QuizReviewItem';

export interface QuizResultPageProps {
  sessionId: string;
}

export function QuizResultPage({ sessionId }: QuizResultPageProps) {
  const { t } = useTranslation('quiz');
  const { data, error, isPending, isError, refetch } = useGetSession(sessionId, { query: { staleTime: Infinity } });

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
    return <Navigate to="/student/quiz/$sessionId" params={{ sessionId }} replace />;
  }

  const reviewed = sortedItems(data).flatMap((item) => (item.attempt ? [{ item, attempt: item.attempt }] : []));
  const lessonId = lessonIdOf(data);
  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('result.title')}</h1>
      <QuizResultSummary session={data} />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.review')}</h2>
      {reviewed.length > 0 ? (
        reviewed.map(({ item, attempt }) => <QuizReviewItem key={item.questionId} item={item} attempt={attempt} />)
      ) : (
        <p className="text-ui text-text-muted">{t('result.none')}</p>
      )}
      {lessonId !== null ? <NewPracticeButton lessonId={lessonId} servedCount={data.items.length} /> : null}
    </section>
  );
}
