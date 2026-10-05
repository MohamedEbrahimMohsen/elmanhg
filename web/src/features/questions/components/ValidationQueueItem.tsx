import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ValidationQueueItemResult } from '@/shared/api/generated/model';
import { formatRelativeTime } from '@/shared/lib/dateTime';
import { formatNumber } from '@/shared/lib/format';
import { stemExcerpt } from '../api/stemExcerpt';

export interface ValidationQueueItemProps {
  item: ValidationQueueItemResult;
  selected: boolean;
  onToggle: (id: string) => void;
  now: Date;
}

export function ValidationQueueItem({ item, selected, onToggle, now }: ValidationQueueItemProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const stem = stemExcerpt(item.stem);
  const meta = [
    t([`types.${item.type}`, item.type]),
    `${item.unitName} › ${item.lessonName}`,
    t([`difficulties.${item.difficulty}`, item.difficulty]),
    t('list.table.versionValue', { version: formatNumber(Number(item.version), lng) }),
    t('validation.queue.age', { age: formatRelativeTime(item.submittedAt, now, lng) }),
  ];

  return (
    <li className="flex items-start gap-3 rounded-md border border-border bg-surface px-3.5 py-3">
      <input
        type="checkbox"
        aria-label={t('validation.queue.select', { stem })}
        checked={selected}
        disabled={!item.openedInSession}
        onChange={() => {
          onToggle(item.id);
        }}
        className="mt-0.5 size-6 shrink-0 accent-accent disabled:opacity-45"
      />
      <div className="flex min-w-0 flex-1 flex-col gap-1">
        <Link
          to="/teacher/q/$questionId"
          params={{ questionId: item.id }}
          className="text-ui font-bold text-text hover:text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {stem}
        </Link>
        <p className="text-caption text-text-muted">{meta.join(' · ')}</p>
      </div>
      {item.openedInSession ? (
        <span className="shrink-0 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-bold text-accent-text">
          {t('validation.queue.opened')}
        </span>
      ) : (
        <span className="shrink-0 text-caption text-text-muted">{t('validation.queue.openToSelect')}</span>
      )}
    </li>
  );
}
