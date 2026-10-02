import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState } from '@/features/content';
import { useSession } from '@/features/session';
import type { UserSummaryResult } from '@/shared/api/generated/model';
import { Pagination } from '@/shared/components/Pagination';
import { Button } from '@/shared/ui/button';
import { hasActiveFilters } from '../api/userListParams';
import { GrantPlanDialog, type GrantPlanTarget } from '../components/GrantPlanDialog';
import { InviteUserDialog } from '../components/InviteUserDialog';
import { TeacherPhoneDialog } from '../components/TeacherPhoneDialog';
import { UserFiltersForm } from '../components/UserFiltersForm';
import { UserListEmptyState } from '../components/UserListEmptyState';
import { UserListSkeleton } from '../components/UserListSkeleton';
import { UserListTable } from '../components/UserListTable';
import { UserRoleTabs } from '../components/UserRoleTabs';
import { UserStatusDialog } from '../components/UserStatusDialog';
import { useUserList } from '../hooks/useUserList';
import { useUsersSearch } from '../hooks/useUsersSearch';
import { registerUsersLocales } from '../locales';

registerUsersLocales();

const whenClosed = (reset: () => void) => (open: boolean) => {
  if (!open) {
    reset();
  }
};

export function UsersPage() {
  const { t } = useTranslation('users');
  const session = useSession();
  const { search, tab, setTab, applyFilters, clearFilters, setPage } = useUsersSearch();
  const { data, error, isPending, isError, refetch } = useUserList(search);
  const [statusTarget, setStatusTarget] = useState<UserSummaryResult | null>(null);
  const [inviteRole, setInviteRole] = useState<'Teacher' | 'Admin' | null>(null);
  const [grantTarget, setGrantTarget] = useState<GrantPlanTarget | null>(null);
  const [phoneTarget, setPhoneTarget] = useState<UserSummaryResult | null>(null);
  const invitableRole = tab === 'teachers' ? 'Teacher' : tab === 'admins' ? 'Admin' : null;

  const renderContent = () => {
    if (isPending) {
      return <UserListSkeleton />;
    }
    if (isError) {
      return <ContentErrorState title={t('list.errorTitle')} error={error} onRetry={() => void refetch()} />;
    }
    if (data.items.length === 0) {
      return (
        <UserListEmptyState variant={hasActiveFilters(search) ? 'no-results' : 'no-data'} onClear={clearFilters} />
      );
    }
    return (
      <>
        <UserListTable
          tab={tab}
          items={data.items}
          currentUserId={session?.userId}
          onSuspend={setStatusTarget}
          onReactivate={setStatusTarget}
          onGrant={(item, plan) => {
            setGrantTarget({ studentId: item.id, displayName: item.displayName, plan });
          }}
          onEditPhone={setPhoneTarget}
        />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <UserRoleTabs value={tab} onChange={setTab} />
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t(`tabs.${tab}`)}</h2>
        {invitableRole ? (
          <Button
            variant="primary"
            onClick={() => {
              setInviteRole(invitableRole);
            }}
          >
            {t(invitableRole === 'Teacher' ? 'invite.teacher' : 'invite.admin')}
          </Button>
        ) : null}
      </div>
      <UserFiltersForm
        key={JSON.stringify([tab, search.q, search.status])}
        search={search}
        onApply={applyFilters}
        onClear={clearFilters}
      />
      {renderContent()}
      <UserStatusDialog
        target={statusTarget}
        onOpenChange={whenClosed(() => {
          setStatusTarget(null);
        })}
      />
      {inviteRole ? (
        <InviteUserDialog
          role={inviteRole}
          onOpenChange={whenClosed(() => {
            setInviteRole(null);
          })}
        />
      ) : null}
      <GrantPlanDialog
        target={grantTarget}
        onOpenChange={whenClosed(() => {
          setGrantTarget(null);
        })}
      />
      <TeacherPhoneDialog
        target={phoneTarget}
        onOpenChange={whenClosed(() => {
          setPhoneTarget(null);
        })}
      />
    </section>
  );
}
