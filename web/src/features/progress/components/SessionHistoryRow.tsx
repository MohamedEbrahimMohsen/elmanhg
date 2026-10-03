import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { SessionHistoryItemResult } from '@/shared/api/generated/model';
import { DateTime } from '@/shared/components/DateTime';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { sessionKindLabelKey, sessionLink } from '../api/sessionHistory';

export interface SessionHistoryRowProps {
  item: SessionHistoryItemResult;
}

const cellClassName = 'px-2.5 py-2.25 text-caption';

export function SessionHistoryRow({ item }: SessionHistoryRowProps) {
  const { t } = useTranslation('progress');
  const link = sessionLink(item);

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cn(cellClassName, 'whitespace-nowrap')}>
        <DateTime value={item.startedAt} relative />
      </td>
      <td className={cellClassName}>{t(sessionKindLabelKey(item.kind))}</td>
      <td className={cellClassName}>{item.scopeName ?? t('history.unknownScope')}</td>
      <td className={cellClassName}>
        {item.submittedAt ? (
          <>
            {t('history.scoreValue', { score: Math.round(Number(item.scorePercent ?? 0)) })}
            {item.isBestScore ? (
              <span className="ms-2 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">
                {t('history.best')}
              </span>
            ) : null}
          </>
        ) : (
          <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
            {t('history.inProgress')}
          </span>
        )}
      </td>
      <td className={cellClassName}>
        {link ? (
          <Button asChild variant="secondary" size="sm">
            <Link to={link.to} params={{ sessionId: item.id }}>
              {t(link.labelKey)}
            </Link>
          </Button>
        ) : null}
      </td>
    </tr>
  );
}
