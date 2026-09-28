import { useTranslation } from 'react-i18next';
import type { SessionHistoryItemResult } from '@/shared/api/generated/model';
import { SessionHistoryRow } from './SessionHistoryRow';

export interface SessionHistoryTableProps {
  items: SessionHistoryItemResult[];
}

const headerKeys = ['date', 'kind', 'scope', 'score', 'actions'] as const;

export function SessionHistoryTable({ items }: SessionHistoryTableProps) {
  const { t } = useTranslation('progress');

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('history.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th
                key={key}
                scope="col"
                className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
              >
                {t(`history.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <SessionHistoryRow key={item.id} item={item} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
