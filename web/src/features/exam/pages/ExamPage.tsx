import { Navigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetExamSession } from '@/shared/api/generated/exams/exams';
import { ExamRunner } from '../components/ExamRunner';

export interface ExamPageProps {
  sessionId: string;
}

export function ExamPage({ sessionId }: ExamPageProps) {
  const { t } = useTranslation('exam');
  const { data, error, isPending, isError, refetch, dataUpdatedAt } = useGetExamSession(sessionId, {
    query: { staleTime: Infinity },
  });

  if (isError) {
    return (
      <ContentErrorState
        title={t('exam.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('exam.loading')} />;
  }
  if (data.submittedAt !== null) {
    return <Navigate to="/student/exam-result/$sessionId" params={{ sessionId }} replace />;
  }
  return <ExamRunner session={data} receivedAt={dataUpdatedAt} />;
}
