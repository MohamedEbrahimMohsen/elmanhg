import { useTranslation } from 'react-i18next';
import type { UserRole, UserStatus } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { useUserStatus } from '../hooks/useUserStatus';

export interface UserStatusTarget {
  id: string;
  displayName: string;
  role: UserRole;
  status: UserStatus;
}

export interface UserStatusDialogProps {
  target: UserStatusTarget | null;
  onOpenChange: (open: boolean) => void;
}

function titleKey(target: UserStatusTarget | null): string {
  if (target?.status !== 'Active') {
    return 'status.reactivateTitle';
  }
  return target.role === 'Student' ? 'status.suspendTitle' : 'status.deactivateTitle';
}

export function UserStatusDialog({ target, onOpenChange }: UserStatusDialogProps) {
  const { t } = useTranslation('users');
  const { suspend, reactivate, isPending } = useUserStatus();
  const suspending = target?.status === 'Active';

  const close = () => {
    onOpenChange(false);
  };
  const confirm = () => {
    if (target) {
      (suspending ? suspend : reactivate)(target.id).then(close, close);
    }
  };

  return (
    <Dialog open={target !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t(titleKey(target), { name: target?.displayName ?? '' })}>
        <p className="text-ui text-text">{t(suspending ? 'status.suspendBody' : 'status.reactivateBody')}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button variant="secondary" onClick={close}>
            {t('status.cancel')}
          </Button>
          <Button
            variant={suspending ? 'danger' : 'primary'}
            disabled={isPending}
            aria-busy={isPending}
            onClick={confirm}
          >
            {t(suspending ? (target.role === 'Student' ? 'status.suspend' : 'status.deactivate') : 'status.reactivate')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
