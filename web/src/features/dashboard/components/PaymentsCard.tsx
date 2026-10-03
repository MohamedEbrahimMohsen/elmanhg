import { useTranslation } from 'react-i18next';
import { useGetDashboardPayments } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardPaymentsParams } from '@/shared/api/generated/model';
import { formatAmount, formatCount } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface PaymentsCardProps {
  params: GetDashboardPaymentsParams;
  subjectSelected: boolean;
}

export function PaymentsCard({ params, subjectSelected }: PaymentsCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardPayments(params);

  return (
    <MetricCard title={t('payments.title')} query={query} variant="panel">
      {(data) => (
        <>
          {subjectSelected ? <p className="text-caption text-text-muted">{t('card.noSubject')}</p> : null}
          <MetricList
            rows={[
              { key: 'succeeded', label: t('payments.succeeded'), value: formatCount(data.succeeded, lng) },
              { key: 'failed', label: t('payments.failed'), value: formatCount(data.failed, lng) },
              {
                key: 'refunds',
                label: t('payments.refunds'),
                value: t('payments.refundsValue', {
                  count: formatCount(data.refunds, lng),
                  amount: formatAmount(data.refunded, lng),
                }),
              },
              { key: 'net', label: t('payments.net'), value: formatAmount(data.netRevenue, lng) },
            ]}
          />
        </>
      )}
    </MetricCard>
  );
}
