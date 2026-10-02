import { useTranslation } from 'react-i18next';
import type { StudentAvatarConversationResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';

export interface DeleteAvatarConversationDialogProps {
  conversation: StudentAvatarConversationResult | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => Promise<void>;
  isPending: boolean;
}

export function DeleteAvatarConversationDialog({
  conversation,
  onOpenChange,
  onConfirm,
  isPending,
}: DeleteAvatarConversationDialogProps) {
  const { t } = useTranslation('avatar');

  return (
    <Dialog open={conversation !== null} onOpenChange={onOpenChange}>
      <DialogContent title={t('deleteDialog.title')}>
        <p className="text-ui text-text">{t('deleteDialog.body')}</p>
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="secondary"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('deleteDialog.cancel')}
          </Button>
          <Button
            variant="danger"
            disabled={isPending}
            aria-busy={isPending}
            onClick={() =>
              void onConfirm()
                .then(() => {
                  onOpenChange(false);
                })
                .catch(() => undefined)
            }
          >
            {t('deleteDialog.confirm')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
