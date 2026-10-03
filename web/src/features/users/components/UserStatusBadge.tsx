import { useTranslation } from 'react-i18next';
import type { UserRole, UserStatus } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';

export interface UserStatusBadgeProps {
  status: UserStatus;
  role: UserRole;
}

export function UserStatusBadge({ status, role }: UserStatusBadgeProps) {
  const { t } = useTranslation('users');
  const active = status === 'Active';
  const label = active
    ? t('status.Active')
    : t(role === 'Student' ? 'status.SuspendedStudent' : 'status.SuspendedStaff');

  return (
    <span
      className={cn(
        'inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold',
        active ? 'bg-success-soft text-success-text' : 'bg-danger-soft text-danger',
      )}
    >
      {label}
    </span>
  );
}
