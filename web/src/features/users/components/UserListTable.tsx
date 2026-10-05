import { useTranslation } from 'react-i18next';
import type { UserSummaryResult } from '@/shared/api/generated/model';
import type { UserListTab } from '../schemas/usersSearchSchema';
import type { GrantablePlan } from './UserActionButtons';
import { UserListRow } from './UserListRow';

export interface UserListTableProps {
  tab: UserListTab;
  items: UserSummaryResult[];
  currentUserId: string | undefined;
  onSuspend: (item: UserSummaryResult) => void;
  onReactivate: (item: UserSummaryResult) => void;
  onGrant: (item: UserSummaryResult, plan: GrantablePlan) => void;
  onEditPhone: (item: UserSummaryResult) => void;
}

const headerKeys: Record<UserListTab, readonly string[]> = {
  students: ['name', 'contact', 'plan', 'status', 'joined', 'actions'],
  teachers: ['name', 'contact', 'subjects', 'status', 'actions'],
  admins: ['name', 'contact', 'status', 'actions'],
};

export function UserListTable({
  tab,
  items,
  currentUserId,
  onSuspend,
  onReactivate,
  onGrant,
  onEditPhone,
}: UserListTableProps) {
  const { t } = useTranslation('users');

  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface shadow-1">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('table.caption')}</caption>
        <thead>
          <tr>
            {headerKeys[tab].map((key) => (
              <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                {t(`table.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <UserListRow
              key={item.id}
              tab={tab}
              item={item}
              currentUserId={currentUserId}
              onSuspend={onSuspend}
              onReactivate={onReactivate}
              onGrant={onGrant}
              onEditPhone={onEditPhone}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
