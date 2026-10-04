import { useTranslation } from 'react-i18next';
import type { SessionHistoryItemResult } from '@/shared/api/generated/model';
import { DateTime } from '@/shared/components/DateTime';
import { cn } from '@/shared/lib/utils';

export interface StudentHistoryTableProps {
  items: SessionHistoryItemResult[];
}

const headerKeys = ['date', 'kind', 'scope', 'score'] as const;
const cellClassName = 'px-2.5 py-2.25 text-caption';

function kindKey(kind: string): string {
  if (kind === 'Quiz') {
    return 'history.kindQuiz';
  }
  return kind === 'UnitExam' ? 'history.kindUnitExam' : 'history.kindMultiUnitExam';
}

export function StudentHistoryTable({ items }: StudentHistoryTableProps) {
  const { t } = useTranslation('users');

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('history.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                {t(`history.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="border-t border-border hover:bg-soft">
              <td className={cn(cellClassName, 'whitespace-nowrap')}>
                <DateTime value={item.startedAt} relative />
              </td>
              <td className={cellClassName}>{t(kindKey(item.kind))}</td>
              <td className={cellClassName}>{item.scopeName ?? t('history.unknownScope')}</td>
              <td className={cellClassName}>
                {item.submittedAt ? (
                  <>
                    {t('history.scoreValue', { score: Math.round(Number(item.scorePercent ?? 0)) })}
                    {item.isBestScore ? (
                      <span className="ms-2 rounded-pill bg-accent-soft px-2.5 py-0.5 text-micro font-bold text-accent-text">
                        {t('history.best')}
                      </span>
                    ) : null}
                  </>
                ) : (
                  <span className="rounded-pill bg-soft px-2.5 py-0.5 text-micro font-bold text-text-muted">
                    {t('history.inProgress')}
                  </span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
