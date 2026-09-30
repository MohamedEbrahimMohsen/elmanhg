import { useTranslation } from 'react-i18next';
import type { UserRole, UserStatus } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export type GrantablePlan = 'Base' | 'AskTeacher';

export interface UserActionTarget {
  id: string;
  role: UserRole;
  status: UserStatus;
  canSuspend: boolean;
  tier: string | null;
  hasAskTeacher: boolean;
}

export interface UserActionButtonsProps {
  target: UserActionTarget;
  currentUserId: string | undefined;
  onSuspend: () => void;
  onReactivate: () => void;
  onGrant: (plan: GrantablePlan) => void;
}

export function grantablePlan(target: Pick<UserActionTarget, 'role' | 'tier' | 'hasAskTeacher'>): GrantablePlan | null {
  if (target.role !== 'Student') {
    return null;
  }
  if (target.tier === 'Free') {
    return 'Base';
  }
  return target.tier === 'Base' && !target.hasAskTeacher ? 'AskTeacher' : null;
}

export function UserActionButtons({ target, currentUserId, onSuspend, onReactivate, onGrant }: UserActionButtonsProps) {
  const { t } = useTranslation('users');
  const plan = grantablePlan(target);
  const active = target.status === 'Active';
  const hint =
    active && !target.canSuspend ? (target.id === currentUserId ? 'status.selfHint' : 'status.lastAdminHint') : null;

  return (
    <div className="flex flex-wrap items-center gap-2">
      {active ? (
        <Button variant="danger" size="sm" disabled={!target.canSuspend} onClick={onSuspend}>
          {t(target.role === 'Student' ? 'status.suspend' : 'status.deactivate')}
        </Button>
      ) : (
        <Button variant="secondary" size="sm" onClick={onReactivate}>
          {t('status.reactivate')}
        </Button>
      )}
      {hint ? <span className="text-caption text-text-muted">{t(hint)}</span> : null}
      {plan ? (
        <Button
          variant="secondary"
          size="sm"
          onClick={() => {
            onGrant(plan);
          }}
        >
          {t(plan === 'Base' ? 'grant.base' : 'grant.askTeacher')}
        </Button>
      ) : null}
    </div>
  );
}
