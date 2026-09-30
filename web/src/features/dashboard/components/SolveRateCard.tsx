import { useTranslation } from 'react-i18next';
import { useGetDashboardSolveRate } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardSolveRateParams } from '@/shared/api/generated/model';
import { formatCount, formatRatio } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface SolveRateCardProps {
  params: GetDashboardSolveRateParams;
}

export function SolveRateCard({ params }: SolveRateCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardSolveRate(params);

  return (
    <MetricCard title={t('solveRate.title')} query={query}>
      {(data) => (
        <KpiFigure value={formatRatio(data.attemptsPerActiveStudentPerDay, lng)} caption={t('solveRate.caption')}>
          <li>{t('solveRate.attempts', { count: formatCount(data.attempts, lng) })}</li>
          <li>{t('solveRate.studentDays', { count: formatCount(data.activeStudentDays, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
