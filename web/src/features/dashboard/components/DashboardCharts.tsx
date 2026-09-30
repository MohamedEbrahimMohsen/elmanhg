import { useTranslation } from 'react-i18next';
import {
  useGetDashboardPayments,
  useGetDashboardSolveRate,
  useGetDashboardValidation,
} from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardPaymentsParams, GetDashboardSolveRateParams } from '@/shared/api/generated/model';
import { formatAmount, formatCount } from '../api/metricFormat';
import { DailyBarChart } from './DailyBarChart';
import { MetricCard } from './MetricCard';

export interface DashboardChartsProps {
  subjectRangeParams: GetDashboardSolveRateParams;
  rangeParams: GetDashboardPaymentsParams;
  subjectSelected: boolean;
}

export function DashboardCharts({ subjectRangeParams, rangeParams, subjectSelected }: DashboardChartsProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const solveRate = useGetDashboardSolveRate(subjectRangeParams);
  const payments = useGetDashboardPayments(rangeParams);
  const validation = useGetDashboardValidation(subjectRangeParams);
  const count = (value: number) => formatCount(value, lng);

  return (
    <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
      <MetricCard title={t('charts.attempts')} query={solveRate} variant="panel">
        {(data) => (
          <DailyBarChart
            label={t('charts.attempts')}
            points={data.daily.map((day) => ({ date: day.date, value: Number(day.attempts) }))}
            formatValue={count}
            emptyText={t('charts.emptyAttempts')}
          />
        )}
      </MetricCard>
      <MetricCard title={t('charts.revenue')} query={payments} variant="panel">
        {(data) => (
          <DailyBarChart
            label={t('charts.revenue')}
            points={data.revenueByDay.map((day) => ({ date: day.date, value: Number(day.value) }))}
            formatValue={(value) => formatAmount({ amountMinor: value, currency: data.revenue.currency }, lng)}
            emptyText={t('charts.emptyRevenue')}
            note={subjectSelected ? t('card.noSubject') : undefined}
          />
        )}
      </MetricCard>
      <MetricCard title={t('charts.decisions')} query={validation} variant="panel">
        {(data) => (
          <DailyBarChart
            label={t('charts.decisions')}
            points={data.dailyDecisions.map((day) => ({ date: day.date, value: Number(day.value) }))}
            formatValue={count}
            emptyText={t('charts.emptyDecisions')}
          />
        )}
      </MetricCard>
    </div>
  );
}
