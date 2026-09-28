import type { UseQueryResult } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import type { ExamAttemptsResult } from '@/shared/api/generated/model';
import { ExamAttemptsTable } from './ExamAttemptsTable';

export interface ExamAttemptsSectionProps {
  query: UseQueryResult<ExamAttemptsResult, unknown>;
  currentSessionId?: string | undefined;
}

export function ExamAttemptsSection({ query, currentSessionId }: ExamAttemptsSectionProps) {
  const { t } = useTranslation('exam');

  if (query.isError) {
    return (
      <ContentErrorState
        title={t('attempts.errorTitle')}
        error={query.error}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  }
  if (query.isPending) {
    return <ContentListSkeleton label={t('attempts.loading')} />;
  }
  return <ExamAttemptsTable attempts={query.data} currentSessionId={currentSessionId} />;
}
