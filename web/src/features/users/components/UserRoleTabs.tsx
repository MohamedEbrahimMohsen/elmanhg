import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { userListTabs, type UserListTab } from '../schemas/usersSearchSchema';

export interface UserRoleTabsProps {
  value: UserListTab;
  onChange: (tab: UserListTab) => void;
}

const tabClassName =
  'inline-flex min-h-11 items-center rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export function UserRoleTabs({ value, onChange }: UserRoleTabsProps) {
  const { t } = useTranslation('users');

  return (
    <div role="tablist" aria-label={t('tabs.label')} className="flex flex-wrap gap-2">
      {userListTabs.map((tab) => (
        <button
          key={tab}
          type="button"
          role="tab"
          aria-selected={value === tab}
          onClick={() => {
            onChange(tab);
          }}
          className={cn(
            tabClassName,
            value === tab
              ? 'border-text bg-text text-surface'
              : 'border-border-strong bg-surface text-text hover:bg-soft',
          )}
        >
          {t(`tabs.${tab}`)}
        </button>
      ))}
    </div>
  );
}
