import { useTranslation } from 'react-i18next';
import { useGetDashboardSuccessRate } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardSuccessRateParams } from '@/shared/api/generated/model';
import { formatCount, formatRate } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface SuccessRateCardProps {
  params: GetDashboardSuccessRateParams;
}

export function SuccessRateCard({ params }: SuccessRateCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardSuccessRate(params);

  return (
    <MetricCard title={t('successRate.title')} query={query}>
      {(data) => (
        <KpiFigure value={formatRate(data.rate, lng)} caption={t('successRate.caption')}>
          <li>
            {t('successRate.correct', {
              correct: formatCount(data.correct, lng),
              attempts: formatCount(data.attempts, lng),
            })}
          </li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
