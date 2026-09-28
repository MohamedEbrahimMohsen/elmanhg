import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { SessionHistoryItemResult } from '@/shared/api/generated/model';
import { formatDate } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { sessionKindLabelKey, sessionLink } from '../api/sessionHistory';

export interface SessionHistoryRowProps {
  item: SessionHistoryItemResult;
}

const cellClassName = 'px-2.5 py-2.25 text-caption';

export function SessionHistoryRow({ item }: SessionHistoryRowProps) {
  const { t, i18n } = useTranslation('progress');
  const link = sessionLink(item);

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        {formatDate(new Date(item.startedAt), i18n.language, 'arabic-indic', {
          dateStyle: 'medium',
          timeStyle: 'short',
        })}
      </td>
      <td className={cellClassName}>{t(sessionKindLabelKey(item.kind))}</td>
      <td className={cellClassName}>{item.scopeName ?? t('history.unknownScope')}</td>
      <td className={cellClassName}>
        {item.submittedAt ? (
          t('history.scoreValue', { score: Math.round(Number(item.scorePercent ?? 0)) })
        ) : (
          <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
            {t('history.inProgress')}
          </span>
        )}
      </td>
      <td className={cellClassName}>
        {link ? (
          <Link
            to={link.to}
            params={{ sessionId: item.id }}
            className="rounded-sm text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {t(link.labelKey)}
          </Link>
        ) : null}
      </td>
    </tr>
  );
}
