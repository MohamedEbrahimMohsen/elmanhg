import { useTranslation } from 'react-i18next';
import { useGetDashboardFunnel } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardFunnelParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed, formatRate } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface FunnelCardProps {
  params: GetDashboardFunnelParams;
  subjectSelected: boolean;
}

export function FunnelCard({ params, subjectSelected }: FunnelCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardFunnel(params);

  return (
    <MetricCard title={t('funnel.title')} query={query}>
      {(data) => (
        <KpiFigure
          value={formatCount(data.completedJourneys, lng)}
          caption={t('funnel.caption')}
          note={subjectSelected ? t('card.noSubject') : undefined}
        >
          {data.steps.map((step) => {
            const values = { step: t(`funnel.steps.${step.type}`), visitors: formatCount(step.visitors, lng) };
            return (
              <li key={step.type}>
                {step.conversionFromPrevious === null
                  ? t('funnel.step', values)
                  : t('funnel.stepConversion', { ...values, rate: formatRate(step.conversionFromPrevious, lng) })}
              </li>
            );
          })}
          <li>{t('funnel.median', { duration: formatElapsed(data.medianLandingToFirstAnswerSeconds, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
