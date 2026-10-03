import { useTranslation } from 'react-i18next';
import { useGetDashboardStudents } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardStudentsParams } from '@/shared/api/generated/model';
import { formatCount } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface StudentsCardProps {
  params: GetDashboardStudentsParams;
  subjectSelected: boolean;
}

const rowKeys = ['newInRange', 'newThisWeek', 'activeToday', 'activeThisMonth'] as const;

export function StudentsCard({ params, subjectSelected }: StudentsCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardStudents(params);

  return (
    <MetricCard title={t('students.title')} query={query} variant="panel">
      {(data) => (
        <>
          {subjectSelected ? <p className="text-caption text-text-muted">{t('card.noSubject')}</p> : null}
          <MetricList
            rows={rowKeys.map((key) => ({
              key,
              label: t(`students.${key}`),
              value: formatCount(data[key], lng),
            }))}
          />
        </>
      )}
    </MetricCard>
  );
}
