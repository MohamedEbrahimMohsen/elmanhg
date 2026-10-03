import { useTranslation } from 'react-i18next';
import { useGetDashboardContent } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardContentParams } from '@/shared/api/generated/model';
import { formatCount } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface ContentCardProps {
  params: GetDashboardContentParams;
}

const rowKeys = [
  'subjects',
  'units',
  'lessonsPublished',
  'lessonsDraft',
  'lessonsArchived',
  'questionsPending',
  'questionsApproved',
  'questionsRejected',
  'questionsRetired',
] as const;

export function ContentCard({ params }: ContentCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardContent(params);

  return (
    <MetricCard title={t('content.title')} query={query} variant="panel">
      {(data) => (
        <>
          <p className="text-caption text-text-muted">{t('card.snapshot')}</p>
          <MetricList
            rows={rowKeys.map((key) => ({ key, label: t(`content.${key}`), value: formatCount(data[key], lng) }))}
          />
        </>
      )}
    </MetricCard>
  );
}
