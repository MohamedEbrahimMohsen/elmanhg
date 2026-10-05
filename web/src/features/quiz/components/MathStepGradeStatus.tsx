import { Clock, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ApiError } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { useMathStepGrade } from '../hooks/useMathStepGrade';
import { MathStepGradeOutcome } from './MathStepGradeOutcome';

export interface MathStepGradeStatusProps {
  sessionId: string;
  questionId: string;
  onGraded?: (() => void) | undefined;
  showOutcome?: boolean;
}

const notFoundCode = 'MATH_STEP_GRADE_NOT_FOUND';

export function MathStepGradeStatus({ sessionId, questionId, onGraded, showOutcome = true }: MathStepGradeStatusProps) {
  const { t } = useTranslation('quiz');
  const { data, error, isPending, isError, refetch } = useMathStepGrade(sessionId, questionId, onGraded);

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="text-caption text-text-muted">
        {t('mathStepGrade.loading')}
      </div>
    );
  }

  if (isError) {
    if (error instanceof ApiError && error.code === notFoundCode) {
      return null;
    }
    return (
      <div
        role="alert"
        className="flex flex-col items-start gap-2 rounded-md border border-danger bg-danger-soft px-3.5 py-3"
      >
        <p className="text-ui font-bold text-danger">{t('mathStepGrade.error')}</p>
        <Button variant="secondary" onClick={() => void refetch()}>
          {t('mathStepGrade.retry')}
        </Button>
      </div>
    );
  }

  if (data.status === 'Pending') {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex items-start gap-3 rounded-md border border-border bg-soft px-3.5 py-3"
      >
        <Loader2 aria-hidden className="size-5 shrink-0 text-text-muted motion-safe:animate-spin" />
        <div>
          <p className="text-ui font-bold text-text">{t('mathStepGrade.pending')}</p>
          <p className="text-caption text-text-muted">{t('mathStepGrade.pendingHint')}</p>
        </div>
      </div>
    );
  }

  if (data.status === 'InReview') {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex items-start gap-3 rounded-md border border-warning bg-warning-soft px-3.5 py-3"
      >
        <Clock aria-hidden className="size-5 shrink-0 text-warning" />
        <div>
          <p className="text-ui font-bold text-text">{t('mathStepGrade.inReview')}</p>
          <p className="text-caption text-text-muted">{t('mathStepGrade.inReviewHint')}</p>
        </div>
      </div>
    );
  }

  return <MathStepGradeOutcome grade={data} showOutcome={showOutcome} />;
}
