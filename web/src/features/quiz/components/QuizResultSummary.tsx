import { useTranslation } from 'react-i18next';
import type { SessionResult } from '@/shared/api/generated/model';
import { hasPendingEssay, isWrittenEssay } from '../api/essayItem';
import { splitDuration } from '../api/quizSession';

export interface QuizResultSummaryProps {
  session: SessionResult;
}

export function QuizResultSummary({ session }: QuizResultSummaryProps) {
  const { t } = useTranslation('quiz');
  const answered = session.items.filter((item) => item.attempt !== null || isWrittenEssay(item)).length;

  return (
    <div className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <p className="font-display text-display font-bold lg:text-display-desktop">
        {t('result.score', { score: Math.round(Number(session.scorePercent ?? 0)) })}
      </p>
      <p className="text-caption text-text-muted">{t('result.answered', { answered, total: session.items.length })}</p>
      <p className="text-caption text-text-muted">
        {t('result.time', splitDuration(Number(session.timeTakenMilliseconds)))}
      </p>
      {hasPendingEssay(session.items) ? (
        <p className="text-caption text-text-muted">{t('result.essaysPending')}</p>
      ) : null}
    </div>
  );
}
