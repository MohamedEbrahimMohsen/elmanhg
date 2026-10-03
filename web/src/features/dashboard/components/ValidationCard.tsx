import { useTranslation } from 'react-i18next';
import { useGetDashboardValidation } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardValidationParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface ValidationCardProps {
  params: GetDashboardValidationParams;
}

export function ValidationCard({ params }: ValidationCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardValidation(params);

  return (
    <MetricCard title={t('validation.title')} query={query} variant="panel">
      {(data) => (
        <>
          <MetricList
            rows={[
              { key: 'approved', label: t('validation.approved'), value: formatCount(data.approved, lng) },
              { key: 'rejected', label: t('validation.rejected'), value: formatCount(data.rejected, lng) },
              {
                key: 'median',
                label: t('validation.median'),
                value: formatElapsed(data.medianSecondsToDecision, lng),
              },
            ]}
          />
          {data.byTeacher.length > 0 ? (
            <>
              <h3 className="text-caption font-semibold text-text-muted">{t('validation.byTeacher')}</h3>
              <MetricList
                rows={data.byTeacher.map((teacher) => ({
                  key: teacher.teacherId,
                  label: teacher.displayName,
                  value: t('validation.teacherValue', {
                    approved: formatCount(teacher.approved, lng),
                    rejected: formatCount(teacher.rejected, lng),
                  }),
                }))}
              />
            </>
          ) : null}
        </>
      )}
    </MetricCard>
  );
}
