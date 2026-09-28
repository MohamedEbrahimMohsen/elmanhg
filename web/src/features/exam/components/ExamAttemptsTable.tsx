import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ExamAttemptsResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';

export interface ExamAttemptsTableProps {
  attempts: ExamAttemptsResult;
  currentSessionId?: string | undefined;
}

const headerKeys = ['date', 'score', 'actions'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

export function ExamAttemptsTable({ attempts, currentSessionId }: ExamAttemptsTableProps) {
  const { t, i18n } = useTranslation('exam');

  if (attempts.attempts.length === 0) {
    return null;
  }
  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('attempts.title')}</h2>
      <p className="text-ui font-semibold">
        {t('attempts.best', { score: Math.round(Number(attempts.bestScorePercent)) })}
      </p>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse">
          <caption className="sr-only">{t('attempts.caption')}</caption>
          <thead>
            <tr>
              {headerKeys.map((key) => (
                <th
                  key={key}
                  scope="col"
                  className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
                >
                  {t(`attempts.${key}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {attempts.attempts.map((attempt) => (
              <tr key={attempt.sessionId} className="border-t border-border">
                <td className={cellClassName}>
                  {formatDate(new Date(attempt.submittedAt), i18n.language, 'arabic-indic', {
                    dateStyle: 'medium',
                    timeStyle: 'short',
                  })}
                </td>
                <td className={cellClassName}>
                  {t('attempts.scoreValue', { score: Math.round(Number(attempt.scorePercent)) })}
                  {attempt.isBest ? (
                    <span className="ms-2 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">
                      {t('attempts.bestBadge')}
                    </span>
                  ) : null}
                </td>
                <td className={cellClassName}>
                  {attempt.sessionId === currentSessionId ? (
                    <span className="text-text-muted">{t('attempts.current')}</span>
                  ) : (
                    <Link
                      to="/student/exam-result/$sessionId"
                      params={{ sessionId: attempt.sessionId }}
                      className="rounded-sm text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
                    >
                      {t('attempts.view')}
                    </Link>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
