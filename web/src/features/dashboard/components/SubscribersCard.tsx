import { useTranslation } from 'react-i18next';
import { useGetDashboardSubscribers } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardSubscribersParams } from '@/shared/api/generated/model';
import { formatAmount, formatCount } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface SubscribersCardProps {
  params: GetDashboardSubscribersParams;
  subjectSelected: boolean;
}

export function SubscribersCard({ params, subjectSelected }: SubscribersCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardSubscribers(params);

  return (
    <MetricCard title={t('subscribers.title')} query={query} variant="panel">
      {(data) => (
        <>
          {subjectSelected ? <p className="text-caption text-text-muted">{t('card.noSubject')}</p> : null}
          <MetricList
            rows={[
              ...data.activeByPlan.map((entry) => ({
                key: entry.plan,
                label: t(`subscribers.plans.${entry.plan}`),
                value: formatCount(entry.count, lng),
              })),
              {
                key: 'churnedInRange',
                label: t('subscribers.churnedInRange'),
                value: formatCount(data.churnedInRange, lng),
              },
              {
                key: 'churnedThisMonth',
                label: t('subscribers.churnedThisMonth'),
                value: formatCount(data.churnedThisMonth, lng),
              },
              { key: 'mrr', label: t('subscribers.mrr'), value: formatAmount(data.monthlyRecurringRevenue, lng) },
            ]}
          />
        </>
      )}
    </MetricCard>
  );
}
