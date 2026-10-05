import { useTranslation } from 'react-i18next';
import type { QuestionListItemResult } from '@/shared/api/generated/model';
import { QuestionRow } from './QuestionRow';

export interface QuestionTableProps {
  items: QuestionListItemResult[];
}

const headerKeys = [
  'question',
  'lesson',
  'type',
  'status',
  'version',
  'teacher',
  'rejectionReason',
  'actions',
] as const;

export function QuestionTable({ items }: QuestionTableProps) {
  const { t } = useTranslation('questions');

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('list.table.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                {t(`list.table.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <QuestionRow key={item.id} item={item} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
