import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetValidationQuestion } from '@/shared/api/generated/validation-queue/validation-queue';
import { toQuestionValues } from '../api/questionValues';
import { ReviewHistoryTable } from '../components/ReviewHistoryTable';
import { ValidationDecisionPanel } from '../components/ValidationDecisionPanel';
import { ValidationQuestionContent } from '../components/ValidationQuestionContent';
import { ValidationQuestionHeader } from '../components/ValidationQuestionHeader';
import { useRecordOpening } from '../hooks/useRecordOpening';
import { useReviewSession } from '../hooks/useReviewSession';

export interface ValidationQuestionPageProps {
  questionId: string;
}

export function ValidationQuestionPage({ questionId }: ValidationQuestionPageProps) {
  const { t } = useTranslation('questions');
  const { data, error, isPending, isError, refetch } = useGetValidationQuestion(questionId);
  const { reviewSessionId } = useReviewSession();
  useRecordOpening(reviewSessionId, data ? { id: data.id, version: Number(data.version) } : undefined);

  if (isError) {
    return (
      <ContentErrorState
        title={t('validation.detail.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('validation.detail.loading')} />;
  }

  const values = toQuestionValues(data);
  return (
    <section className="flex flex-col gap-4">
      <ValidationQuestionHeader question={data} />
      <ValidationQuestionContent question={data} values={values} />
      {data.validationStatus === 'Pending' && !data.retiredAt ? (
        <ValidationDecisionPanel key={`${data.id}-${String(data.version)}`} question={data} />
      ) : null}
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('validation.history.title')}</h2>
      <ReviewHistoryTable revisions={data.revisions} decisions={data.decisions} />
    </section>
  );
}
