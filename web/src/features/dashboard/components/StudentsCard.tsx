import { useTranslation } from 'react-i18next';
import { useGetDashboardStudents } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardStudentsParams } from '@/shared/api/generated/model';
import { formatCount } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface StudentsCardProps {
  params: GetDashboardStudentsParams;
  subjectSelected: boolean;
}

export function StudentsCard({ params, subjectSelected }: StudentsCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardStudents(params);

  return (
    <MetricCard title={t('students.title')} query={query}>
      {(data) => (
        <KpiFigure
          value={formatCount(data.total, lng)}
          caption={t('students.caption')}
          note={subjectSelected ? t('card.noSubject') : undefined}
        >
          <li>{t('students.newInRange', { count: formatCount(data.newInRange, lng) })}</li>
          <li>{t('students.newThisWeek', { count: formatCount(data.newThisWeek, lng) })}</li>
          <li>{t('students.activeToday', { count: formatCount(data.activeToday, lng) })}</li>
          <li>{t('students.activeThisMonth', { count: formatCount(data.activeThisMonth, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
