import { useTranslation } from 'react-i18next';
import type { TeacherInboxFilter } from '@/shared/api/generated/model';
import { pillTabClassName } from '@/shared/ui/pillTab';

export interface InboxFilterTabsProps {
  filter: TeacherInboxFilter;
  onChange: (filter: TeacherInboxFilter) => void;
}

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
          className={pillTabClassName}
        >
          {t(`inbox.filters.${value}`)}
        </button>
      ))}
    </div>
  );
}
