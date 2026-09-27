import { useTranslation } from 'react-i18next';
import type { AuditLogResult } from '@/shared/api/generated/model';
import { AuditLogRow } from './AuditLogRow';

export interface AuditLogTableProps {
  items: AuditLogResult[];
}

const headerKeys = ['time', 'actor', 'action', 'entity', 'entityId', 'details'] as const;

export function AuditLogTable({ items }: AuditLogTableProps) {
  const { t } = useTranslation('audit');

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('table.caption')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th
                key={key}
                scope="col"
                className="px-2.5 py-2.25 text-start text-caption font-semibold text-text-muted"
              >
                {t(`table.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <AuditLogRow key={item.id} item={item} />
          ))}
        </tbody>
      </table>
    </div>
  );
}
