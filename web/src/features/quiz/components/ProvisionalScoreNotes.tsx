import { useTranslation } from 'react-i18next';
import { hasPendingEssay, type EssayItem } from '../api/essayItem';
import { hasPendingMathSteps, type MathStepsItem } from '../api/mathStepsItem';
import { countAwaitingReview } from '../api/pendingGrades';

export interface ProvisionalScoreNotesProps {
  items: readonly (EssayItem & MathStepsItem)[];
}

export function ProvisionalScoreNotes({ items }: ProvisionalScoreNotesProps) {
  const { t } = useTranslation('quiz');
  const inReview = countAwaitingReview(items);

  return (
    <>
      {inReview > 0 ? (
        <p role="status" className="rounded-md border border-warning bg-warning-soft px-3 py-2 text-caption text-text">
          {t('result.inReview', { count: inReview })}
        </p>
      ) : null}
      {hasPendingEssay(items) ? <p className="text-caption text-text-muted">{t('result.essaysPending')}</p> : null}
      {hasPendingMathSteps(items) ? <p className="text-caption text-text-muted">{t('result.mathPending')}</p> : null}
    </>
  );
}
