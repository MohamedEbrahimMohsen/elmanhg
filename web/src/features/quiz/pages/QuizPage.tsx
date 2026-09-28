import { Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSession } from '@/shared/api/generated/sessions/sessions';
import { QuizRunner } from '../components/QuizRunner';

export interface QuizPageProps {
  sessionId: string;
}

export function QuizPage({ sessionId }: QuizPageProps) {
  const { t } = useTranslation('quiz');
  const { data, error, isPending, isError, refetch } = useGetSession(sessionId, { query: { staleTime: Infinity } });

  if (isError) {
    return (
      <ContentErrorState
        title={t('session.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('session.loading')} />;
  }
  if (data.submittedAt !== null) {
    return <Navigate to="/student/quiz-result/$sessionId" params={{ sessionId }} replace />;
  }
  return <QuizRunner session={data} />;
}
