import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SuccessRateMetricsResult } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { formatCount, formatRate } from '../api/metricFormat';

export interface SuccessRateBreakdownProps {
  data: SuccessRateMetricsResult;
  subjectSelected: boolean;
}

type Level = 'subject' | 'unit' | 'lesson';

const levels: readonly Level[] = ['subject', 'unit', 'lesson'];

const headerCell = 'px-2.5 py-2 text-start text-caption font-semibold text-text-muted';
const bodyCell = 'px-2.5 py-2 text-caption text-text';

export function SuccessRateBreakdown({ data, subjectSelected }: SuccessRateBreakdownProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const [level, setLevel] = useState<Level>('subject');
  const rows = level === 'subject' ? data.bySubject : level === 'unit' ? data.byUnit : data.byLesson;
  const message =
    level === 'lesson' && !subjectSelected
      ? t('breakdown.chooseSubject')
      : rows.length === 0
        ? t('breakdown.empty')
        : null;

  return (
    <>
      <div role="group" aria-label={t('breakdown.levelLabel')} className="flex gap-2">
        {levels.map((option) => (
          <button
            key={option}
            type="button"
            aria-pressed={level === option}
            onClick={() => {
              setLevel(option);
            }}
            className={cn(
              'min-h-9 rounded-full px-3.5 text-ui focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden',
              level === option
                ? 'border border-border-strong bg-surface font-semibold text-text'
                : 'text-text-muted hover:bg-soft',
            )}
          >
            {t(`breakdown.levels.${option}`)}
          </button>
        ))}
      </div>
      {message === null ? (
        <div className="overflow-x-auto">
          <table className="w-full text-start">
            <caption className="sr-only">{t(`breakdown.levels.${level}`)}</caption>
            <thead>
              <tr>
                <th scope="col" className={headerCell}>
                  {t('breakdown.name')}
                </th>
                <th scope="col" className={headerCell}>
                  {t('breakdown.attempts')}
                </th>
                <th scope="col" className={headerCell}>
                  {t('breakdown.correct')}
                </th>
                <th scope="col" className={headerCell}>
                  {t('breakdown.rate')}
                </th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.id} className="border-t border-border">
                  <td className={bodyCell}>{row.name}</td>
                  <td className={bodyCell}>{formatCount(row.attempts, lng)}</td>
                  <td className={bodyCell}>{formatCount(row.correct, lng)}</td>
                  <td className={bodyCell}>{formatRate(row.rate, lng)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="text-caption text-text-muted">{message}</p>
      )}
    </>
  );
}
