import { useTranslation } from 'react-i18next';
import {
  useGetDashboardContent,
  useGetDashboardPayments,
  useGetDashboardStudents,
  useGetDashboardSubscribers,
} from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardContentParams } from '@/shared/api/generated/model';
import type { DashboardRange } from '../api/dashboardRange';
import { formatAmount, formatCount } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface GrowthTilesProps {
  range: DashboardRange;
  contentParams: GetDashboardContentParams;
  subjectSelected: boolean;
}

export function GrowthTiles({ range, contentParams, subjectSelected }: GrowthTilesProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const students = useGetDashboardStudents(range);
  const subscribers = useGetDashboardSubscribers(range);
  const payments = useGetDashboardPayments(range);
  const content = useGetDashboardContent(contentParams);
  const note = subjectSelected ? t('card.noSubject') : undefined;

  return (
    <>
      <MetricCard title={t('tiles.students')} query={students}>
        {(data) => (
          <KpiFigure
            value={formatCount(data.total, lng)}
            caption={t('tiles.studentsDetail', { count: formatCount(data.newInRange, lng) })}
            note={note}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.subscribers')} query={subscribers}>
        {(data) => (
          <KpiFigure
            value={formatCount(data.activeSubscriptions, lng)}
            caption={t('tiles.subscribersDetail', { count: formatCount(data.churnedInRange, lng) })}
            note={note}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.revenue')} query={payments}>
        {(data) => (
          <KpiFigure
            value={formatAmount(data.revenue, lng)}
            caption={t('tiles.revenueDetail', { amount: formatAmount(data.netRevenue, lng) })}
            note={note}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.content')} query={content}>
        {(data) => <KpiFigure value={formatCount(data.servableTotal, lng)} caption={t('card.snapshot')} />}
      </MetricCard>
    </>
  );
}
