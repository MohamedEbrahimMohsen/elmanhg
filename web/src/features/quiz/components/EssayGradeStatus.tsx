import { Clock, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { useEssayGrade } from '../hooks/useEssayGrade';
import { EssayGradeOutcome } from './EssayGradeOutcome';

export interface EssayGradeStatusProps {
  sessionId: string;
  questionId: string;
  onGraded?: (() => void) | undefined;
}

export function EssayGradeStatus({ sessionId, questionId, onGraded }: EssayGradeStatusProps) {
  const { t } = useTranslation('quiz');
  const { data, isPending, isError, refetch } = useEssayGrade(sessionId, questionId, onGraded);

  if (isPending) {
    return (
      <div role="status" aria-busy="true" className="text-caption text-text-muted">
        {t('essayGrade.loading')}
      </div>
    );
  }

  if (isError) {
    return (
      <div
        role="alert"
        className="flex flex-col items-start gap-2 rounded-md border border-danger bg-danger-soft px-3.5 py-3"
      >
        <p className="text-ui font-bold text-danger">{t('essayGrade.error')}</p>
        <Button variant="secondary" onClick={() => void refetch()}>
          {t('essayGrade.retry')}
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
          <p className="text-ui font-bold text-text">{t('essayGrade.pending')}</p>
          <p className="text-caption text-text-muted">{t('essayGrade.pendingHint')}</p>
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
          <p className="text-ui font-bold text-text">{t('essayGrade.inReview')}</p>
          <p className="text-caption text-text-muted">{t('essayGrade.inReviewHint')}</p>
        </div>
      </div>
    );
  }

  return <EssayGradeOutcome grade={data} />;
}
