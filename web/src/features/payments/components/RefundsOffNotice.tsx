import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { useRefundsEnabled } from '../hooks/useRefundsEnabled';

export const refundsOffNoticeId = 'refunds-off-notice';

export function RefundsOffNotice() {
  const { t } = useTranslation('payments');
  const { data: refundsEnabled, isError, refetch } = useRefundsEnabled();

  if (isError) {
    return (
      <div
        role="alert"
        className="flex flex-wrap items-center gap-3 rounded-lg border border-danger bg-danger-soft p-4"
      >
        <p className="text-caption text-danger">{t('refundsOff.loadError')}</p>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => {
            void refetch();
          }}
        >
          {t('common:actions.retry')}
        </Button>
      </div>
    );
  }

  if (refundsEnabled !== false) {
    return null;
  }

  return (
    <div
      id={refundsOffNoticeId}
      role="note"
      aria-labelledby={`${refundsOffNoticeId}-title`}
      className="flex flex-col gap-1 rounded-lg border border-border bg-warning-soft p-4"
    >
      <p id={`${refundsOffNoticeId}-title`} className="text-ui font-semibold text-text">
        {t('refundsOff.title')}
      </p>
      <p className="text-caption text-text">{t('refundsOff.body')}</p>
    </div>
  );
}
