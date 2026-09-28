import { useTranslation } from 'react-i18next';
import type { ExamSessionResult } from '@/shared/api/generated/model';
import { splitDuration } from '../api/examSession';

export interface ExamResultSummaryProps {
  session: ExamSessionResult;
}

const badgeClassName = 'self-start rounded-full px-2.5 py-0.5 text-micro font-semibold text-surface';

export function ExamResultSummary({ session }: ExamResultSummaryProps) {
  const { t } = useTranslation('exam');
  const answered = session.items.filter((item) => item.attempt !== null).length;

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <p className="font-display text-display font-bold lg:text-display-desktop">
        {t('result.score', { score: Math.round(Number(session.scorePercent ?? 0)) })}
      </p>
      {session.isPassed ? (
        <span className={`${badgeClassName} bg-success`}>{t('result.passed')}</span>
      ) : (
        <span className={`${badgeClassName} bg-danger`}>
          {t('result.failed', { passMark: Number(session.passMark) })}
        </span>
      )}
      <p className="text-caption text-text-muted">{t('result.answered', { answered, total: session.items.length })}</p>
      <p className="text-caption text-text-muted">
        {t('result.time', splitDuration(Number(session.elapsedMilliseconds)))}
      </p>
    </div>
  );
}
