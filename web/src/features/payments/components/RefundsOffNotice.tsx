import { useTranslation } from 'react-i18next';
import { useRefundsEnabled } from '../hooks/useRefundsEnabled';

export const refundsOffNoticeId = 'refunds-off-notice';

export function RefundsOffNotice() {
  const { t } = useTranslation('payments');
  const { data: refundsEnabled } = useRefundsEnabled();

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
