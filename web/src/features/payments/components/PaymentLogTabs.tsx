import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import type { PaymentLogView } from '../schemas/paymentLogSearchSchema';

export interface PaymentLogTabsProps {
  view: PaymentLogView;
  reviewCount: number;
  onChange: (view: PaymentLogView) => void;
}

const tabClassName =
  'inline-flex min-h-11 items-center gap-2 rounded-pill border px-4 text-ui font-semibold focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

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
          className={cn(
            tabClassName,
            view === tab
              ? 'border-text bg-text text-surface'
              : 'border-border-strong bg-surface text-text hover:bg-soft',
          )}
        >
          {t(`tabs.${tab}`)}
          {tab === 'review' && reviewCount > 0 ? (
            <span className="rounded-pill bg-soft px-2 py-0.5 text-micro font-semibold text-text-muted">
              {formatNumber(reviewCount, lng, 'latin')}
            </span>
          ) : null}
        </button>
      ))}
    </div>
  );
}
