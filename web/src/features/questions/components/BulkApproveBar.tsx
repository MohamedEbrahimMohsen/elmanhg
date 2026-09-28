import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';

export interface BulkApproveBarProps {
  count: number;
  isPending: boolean;
  onConfirm: () => Promise<void>;
}

export function BulkApproveBar({ count, isPending, onConfirm }: BulkApproveBarProps) {
  const { t, i18n } = useTranslation('questions');
  const [open, setOpen] = useState(false);
  const formattedCount = formatNumber(count, i18n.resolvedLanguage ?? i18n.language, 'latin');

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <p aria-live="polite" className="text-caption text-text-muted">
        {t('validation.queue.selectedCount', { count, formattedCount })}
      </p>
      <Dialog open={open} onOpenChange={setOpen}>
        <Button
          variant="primary"
          disabled={count === 0 || isPending}
          onClick={() => {
            setOpen(true);
          }}
        >
          {t('validation.queue.bulkApprove')}
        </Button>
        <DialogContent title={t('validation.queue.confirmTitle')}>
          <p className="text-ui text-text">{t('validation.queue.confirmBody', { count, formattedCount })}</p>
          <div className="flex flex-wrap justify-end gap-3">
            <Button
              variant="secondary"
              onClick={() => {
                setOpen(false);
              }}
            >
              {t('validation.queue.cancel')}
            </Button>
            <Button
              variant="primary"
              disabled={isPending}
              onClick={() => {
                setOpen(false);
                void onConfirm();
              }}
            >
              {t('validation.queue.confirm')}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
