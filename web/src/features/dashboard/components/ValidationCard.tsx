import { useTranslation } from 'react-i18next';
import { useGetDashboardValidation } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardValidationParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface ValidationCardProps {
  params: GetDashboardValidationParams;
}

export function ValidationCard({ params }: ValidationCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardValidation(params);

  return (
    <MetricCard title={t('validation.title')} query={query}>
      {(data) => (
        <KpiFigure value={formatCount(data.pendingBacklog, lng)} caption={t('validation.caption')}>
          <li>{t('validation.approved', { count: formatCount(data.approved, lng) })}</li>
          <li>{t('validation.rejected', { count: formatCount(data.rejected, lng) })}</li>
          <li>{t('validation.median', { duration: formatElapsed(data.medianSecondsToDecision, lng) })}</li>
          {data.byTeacher.map((teacher) => (
            <li key={teacher.teacherId}>
              {t('validation.teacher', {
                name: teacher.displayName,
                approved: formatCount(teacher.approved, lng),
                rejected: formatCount(teacher.rejected, lng),
              })}
            </li>
          ))}
        </KpiFigure>
      )}
    </MetricCard>
  );
}
