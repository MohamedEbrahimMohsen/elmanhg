import { useTranslation } from 'react-i18next';
import type { QuestionDecisionResult, QuestionRevisionEntryResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';
import { buildReviewHistory, type ReviewHistoryEntry } from '../api/reviewHistory';

export interface ReviewHistoryTableProps {
  revisions: QuestionRevisionEntryResult[];
  decisions: QuestionDecisionResult[];
}

const headerClassName = 'px-2.5 py-2.25 text-start text-caption font-bold text-text-muted';
const cellClassName = 'px-2.5 py-2.25 align-top text-caption';

export function ReviewHistoryTable({ revisions, decisions }: ReviewHistoryTableProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const entries = buildReviewHistory(revisions, decisions);
  const difficultyLabel = (value: string) => t([`difficulties.${value}`, value]);
  const details = (entry: ReviewHistoryEntry) =>
    [
      entry.reason,
      entry.difficultyChangedFrom && entry.difficulty
        ? t('validation.history.difficultyChanged', {
            from: difficultyLabel(entry.difficultyChangedFrom),
            to: difficultyLabel(entry.difficulty),
          })
        : null,
    ]
      .filter((value) => value !== null)
      .join(' · ');

  if (entries.length === 0) {
    return null;
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('validation.history.caption')}</caption>
        <thead>
          <tr>
            <th scope="col" className={headerClassName}>
              {t('validation.history.date')}
            </th>
            <th scope="col" className={headerClassName}>
              {t('validation.history.by')}
            </th>
            <th scope="col" className={headerClassName}>
              {t('validation.history.action')}
            </th>
            <th scope="col" className={headerClassName}>
              {t('validation.history.details')}
            </th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.key} className="border-t border-border hover:bg-soft">
              <td className={cellClassName}>{formatDateTime(entry.at, lng)}</td>
              <td className={cellClassName}>{entry.actorName ?? t('validation.history.noActor')}</td>
              <td className={cellClassName}>
                {`${t(`validation.history.kind.${entry.kind}`)} · ${t('validation.history.versionValue', { version: formatNumber(entry.version, lng) })}`}
              </td>
              <td className={cellClassName}>{details(entry)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
