import { useTranslation } from 'react-i18next';
import { useGetDashboardFunnel } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardFunnelParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed, formatRate } from '../api/metricFormat';
import { BarList } from './BarList';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface FunnelCardProps {
  params: GetDashboardFunnelParams;
  subjectSelected: boolean;
}

export function FunnelCard({ params, subjectSelected }: FunnelCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardFunnel(params);

  return (
    <MetricCard title={t('funnel.title')} query={query} variant="panel">
      {(data) => {
        const visitors = Number(data.steps.at(0)?.visitors ?? 0);
        return (
          <>
            {subjectSelected ? <p className="text-caption text-text-muted">{t('card.noSubject')}</p> : null}
            {visitors === 0 ? (
              <p className="text-caption text-text-muted">{t('funnel.empty')}</p>
            ) : (
              <>
                <p className="text-caption text-text-muted">
                  {t('funnel.completed', { count: formatCount(data.completedJourneys, lng) })}
                </p>
                <BarList
                  max={visitors}
                  items={data.steps.map((step) => ({
                    key: step.type,
                    label: t(`funnel.steps.${step.type}`),
                    value: Number(step.visitors),
                    display:
                      step.conversionFromPrevious === null
                        ? formatCount(step.visitors, lng)
                        : t('funnel.stepValue', {
                            visitors: formatCount(step.visitors, lng),
                            rate: formatRate(step.conversionFromPrevious, lng),
                          }),
                  }))}
                />
                <MetricList
                  rows={[
                    {
                      key: 'median',
                      label: t('funnel.median'),
                      value: formatElapsed(data.medianLandingToFirstAnswerSeconds, lng),
                    },
                  ]}
                />
              </>
            )}
          </>
        );
      }}
    </MetricCard>
  );
}
