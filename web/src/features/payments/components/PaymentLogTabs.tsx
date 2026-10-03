import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { pillTabClassName } from '@/shared/ui/pillTab';
import type { PaymentLogView } from '../schemas/paymentLogSearchSchema';

export interface PaymentLogTabsProps {
  view: PaymentLogView;
  reviewCount: number;
  onChange: (view: PaymentLogView) => void;
}

export function PaymentLogTabs({ view, reviewCount, onChange }: PaymentLogTabsProps) {
  const { t, i18n } = useTranslation('payments');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const tabs: PaymentLogView[] = ['all', 'review'];

  return (
    <div className="flex flex-wrap gap-2">
      {tabs.map((tab) => (
        <button
          key={tab}
          type="button"
          aria-pressed={view === tab}
          onClick={() => {
            onChange(tab);
          }}
          className={pillTabClassName}
        >
          {t(`tabs.${tab}`)}
          {tab === 'review' && reviewCount > 0 ? (
            <span className="rounded-pill bg-soft px-2 py-0.5 text-micro font-semibold text-text-muted">
              {formatNumber(reviewCount, lng)}
            </span>
          ) : null}
        </button>
      ))}
    </div>
  );
}
