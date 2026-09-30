import { useTranslation } from 'react-i18next';
import { useGetDashboardPayments } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardPaymentsParams } from '@/shared/api/generated/model';
import { formatAmount, formatCount } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface PaymentsCardProps {
  params: GetDashboardPaymentsParams;
  subjectSelected: boolean;
}

export function PaymentsCard({ params, subjectSelected }: PaymentsCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardPayments(params);

  return (
    <MetricCard title={t('payments.title')} query={query}>
      {(data) => (
        <KpiFigure
          value={formatAmount(data.revenue, lng)}
          caption={t('payments.caption')}
          note={subjectSelected ? t('card.noSubject') : undefined}
        >
          <li>{t('payments.succeeded', { count: formatCount(data.succeeded, lng) })}</li>
          <li>{t('payments.failed', { count: formatCount(data.failed, lng) })}</li>
          <li>
            {t('payments.refunds', {
              count: formatCount(data.refunds, lng),
              amount: formatAmount(data.refunded, lng),
            })}
          </li>
          <li>{t('payments.net', { amount: formatAmount(data.netRevenue, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
