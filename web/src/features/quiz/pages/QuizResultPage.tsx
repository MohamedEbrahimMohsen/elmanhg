import { useQueryClient } from '@tanstack/react-query';
import { Link, Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { invalidateMastery } from '@/features/mastery';
import { getGetSessionQueryKey, useGetSession } from '@/shared/api/generated/sessions/sessions';
import { Button } from '@/shared/ui/button';
import { isWrittenEssay } from '../api/essayItem';
import { isPendingMathSteps } from '../api/mathStepsItem';
import { lessonIdOf, sortedItems } from '../api/quizSession';
import { EssayReviewItem } from '../components/EssayReviewItem';
import { MathStepsReviewItem } from '../components/MathStepsReviewItem';
import { NewPracticeButton } from '../components/NewPracticeButton';
import { QuizResultSummary } from '../components/QuizResultSummary';
import { QuizReviewItem } from '../components/QuizReviewItem';

export interface QuizResultPageProps {
  sessionId: string;
}

export function QuizResultPage({ sessionId }: QuizResultPageProps) {
  const { t } = useTranslation('quiz');
  const queryClient = useQueryClient();
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

  const reviewed = sortedItems(data).filter(
    (item) => item.attempt !== null || isWrittenEssay(item) || isPendingMathSteps(item),
  );
  const refreshSession = () => {
    void queryClient.invalidateQueries({ queryKey: getGetSessionQueryKey(sessionId) });
    void invalidateMastery(queryClient);
  };
  const lessonId = lessonIdOf(data);
  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('result.title')}</h1>
      <QuizResultSummary session={data} />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('result.review')}</h2>
      {reviewed.length > 0 ? (
        reviewed.map((item) =>
          isWrittenEssay(item) ? (
            <EssayReviewItem key={item.questionId} sessionId={sessionId} item={item} onGraded={refreshSession} />
          ) : item.attempt === null ? (
            <MathStepsReviewItem key={item.questionId} sessionId={sessionId} item={item} onGraded={refreshSession} />
          ) : (
            <QuizReviewItem
              key={item.questionId}
              item={item}
              attempt={item.attempt}
              ask={{
                entryPoint: 'QuizQuestion',
                sessionId,
                questionId: item.questionId,
                title: t('avatar.questionTitle', { position: Number(item.position) }),
              }}
            />
          ),
        )
      ) : (
        <p className="text-ui text-text-muted">{t('result.none')}</p>
      )}
      {lessonId !== null ? <NewPracticeButton lessonId={lessonId} servedCount={data.items.length} /> : null}
      {lessonId !== null ? (
        <Button asChild variant="secondary" className="self-start">
          <Link to="/student/lesson/$lessonId" params={{ lessonId }}>
            {t('result.backToLesson')}
          </Link>
        </Button>
      ) : null}
    </section>
  );
}
