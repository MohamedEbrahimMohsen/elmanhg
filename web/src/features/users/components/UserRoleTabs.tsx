import { useTranslation } from 'react-i18next';
import { pillTabClassName } from '@/shared/ui/pillTab';
import { userListTabs, type UserListTab } from '../schemas/usersSearchSchema';

export interface UserRoleTabsProps {
  value: UserListTab;
  onChange: (tab: UserListTab) => void;
}

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
          className={pillTabClassName}
        >
          {t(`tabs.${tab}`)}
        </button>
      ))}
    </div>
  );
}
