import { useTranslation } from 'react-i18next';
import type { TeacherInboxFilter } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';

export interface InboxFilterTabsProps {
  filter: TeacherInboxFilter;
  onChange: (filter: TeacherInboxFilter) => void;
}

const tabClassName =
  'inline-flex min-h-11 items-center gap-2 rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

const filters: TeacherInboxFilter[] = ['All', 'Unclaimed', 'Mine'];

export function InboxFilterTabs({ filter, onChange }: InboxFilterTabsProps) {
  const { t } = useTranslation('askTeacher');

  return (
    <div role="group" aria-label={t('inbox.filters.label')} className="flex flex-wrap gap-2">
      {filters.map((value) => (
        <button
          key={value}
          type="button"
          aria-pressed={filter === value}
          onClick={() => {
            onChange(value);
          }}
          className={cn(
            tabClassName,
            filter === value
              ? 'border-text bg-text text-surface'
              : 'border-border-strong bg-surface text-text hover:bg-soft',
          )}
        >
          {t(`inbox.filters.${value}`)}
        </button>
      ))}
    </div>
  );
}
