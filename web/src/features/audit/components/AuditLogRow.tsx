import { useTranslation } from 'react-i18next';
import type { AuditLogResult } from '@/shared/api/generated/model';
import { DateTime } from '@/shared/components/DateTime';
import { cn } from '@/shared/lib/utils';
import { formatDiff } from './formatDiff';
import { OutcomeBadge } from './OutcomeBadge';

export interface AuditLogRowProps {
  item: AuditLogResult;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';
const monoClassName = 'font-mono text-mono';

export function AuditLogRow({ item }: AuditLogRowProps) {
  const { t } = useTranslation('audit');

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        <DateTime value={item.timestamp} relative />
      </td>
      <td className={cellClassName}>
        <bdi dir="ltr">{item.actorUserName ?? t('table.system')}</bdi>
        {item.actorRole ? <span className="block text-caption text-text-muted">{item.actorRole}</span> : null}
      </td>
      <td className={cellClassName}>
        <span dir="ltr" className={monoClassName}>
          {item.action}
        </span>
      </td>
      <td className={cellClassName}>
        <span dir="ltr" className={monoClassName}>
          {item.resourceType}
        </span>
      </td>
      <td className={cellClassName}>
        <span dir="ltr" className={cn(monoClassName, 'break-all')}>
          {item.resourceId}
        </span>
      </td>
      <td className={cellClassName}>
        <div className="flex flex-col items-start gap-1.5">
          <OutcomeBadge outcome={item.outcome} />
          {item.errorCode ? (
            <span dir="ltr" className={monoClassName}>
              {item.errorCode}
            </span>
          ) : null}
          {item.diff ? (
            <details>
              <summary className="cursor-pointer text-accent">{t('table.changes')}</summary>
              <pre dir="ltr" className="font-mono text-mono whitespace-pre-wrap">
                {formatDiff(item.diff)}
              </pre>
            </details>
          ) : null}
        </div>
      </td>
    </tr>
  );
}
