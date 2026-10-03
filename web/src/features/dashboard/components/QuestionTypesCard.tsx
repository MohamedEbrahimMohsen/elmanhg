import { useTranslation } from 'react-i18next';
import { useGetDashboardContent } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardContentParams } from '@/shared/api/generated/model';
import { formatCount, formatRate } from '../api/metricFormat';
import { BarList } from './BarList';
import { MetricCard } from './MetricCard';

export interface QuestionTypesCardProps {
  params: GetDashboardContentParams;
}

export function QuestionTypesCard({ params }: QuestionTypesCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardContent(params);

  return (
    <MetricCard title={t('questionTypes.title')} query={query} variant="panel">
      {(data) => {
        const total = data.questionsByType.reduce((sum, entry) => sum + Number(entry.count), 0);
        return total === 0 ? (
          <p className="text-caption text-text-muted">{t('questionTypes.empty')}</p>
        ) : (
          <BarList
            max={total}
            items={data.questionsByType.map((entry) => ({
              key: entry.type,
              label: t(`questions:types.${entry.type}`),
              value: Number(entry.count),
              display: t('questionTypes.share', {
                count: formatCount(entry.count, lng),
                share: formatRate(Number(entry.count) / total, lng),
              }),
            }))}
          />
        );
      }}
    </MetricCard>
  );
}
