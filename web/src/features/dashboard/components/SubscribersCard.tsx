import { useTranslation } from 'react-i18next';
import { useGetDashboardSubscribers } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardSubscribersParams } from '@/shared/api/generated/model';
import { formatAmount, formatCount } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface SubscribersCardProps {
  params: GetDashboardSubscribersParams;
  subjectSelected: boolean;
}

export function SubscribersCard({ params, subjectSelected }: SubscribersCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardSubscribers(params);

  return (
    <MetricCard title={t('subscribers.title')} query={query}>
      {(data) => (
        <KpiFigure
          value={formatCount(data.activeSubscriptions, lng)}
          caption={t('subscribers.caption')}
          note={subjectSelected ? t('card.noSubject') : undefined}
        >
          {data.activeByPlan.map((entry) => (
            <li key={entry.plan}>
              {t('subscribers.plan', {
                plan: t(`subscribers.plans.${entry.plan}`),
                count: formatCount(entry.count, lng),
              })}
            </li>
          ))}
          <li>{t('subscribers.churnedInRange', { count: formatCount(data.churnedInRange, lng) })}</li>
          <li>{t('subscribers.churnedThisMonth', { count: formatCount(data.churnedThisMonth, lng) })}</li>
          <li>{t('subscribers.mrr', { amount: formatAmount(data.monthlyRecurringRevenue, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
