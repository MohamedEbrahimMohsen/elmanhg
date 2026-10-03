import { useTranslation } from 'react-i18next';
import { useGetDashboardAskTeacher } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardAskTeacherParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed, formatRate } from '../api/metricFormat';
import { MetricCard } from './MetricCard';
import { MetricList } from './MetricList';

export interface AskTeacherCardProps {
  params: GetDashboardAskTeacherParams;
}

export function AskTeacherCard({ params }: AskTeacherCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardAskTeacher(params);

  return (
    <MetricCard title={t('askTeacher.title')} query={query} variant="panel">
      {(data) => (
        <MetricList
          rows={[
            { key: 'awaiting', label: t('askTeacher.awaiting'), value: formatCount(data.awaitingReply, lng) },
            {
              key: 'overdue',
              label: t('askTeacher.overdue'),
              value: formatCount(data.overdueNow, lng),
              tone: Number(data.overdueNow) > 0 ? 'danger' : undefined,
            },
            { key: 'breaches', label: t('askTeacher.breaches'), value: formatCount(data.slaBreaches, lng) },
            {
              key: 'replies',
              label: t('askTeacher.replies'),
              value: t('askTeacher.repliesValue', {
                count: formatCount(data.replies, lng),
                within: formatCount(data.repliedWithinSla, lng),
              }),
            },
            { key: 'compliance', label: t('askTeacher.compliance'), value: formatRate(data.slaComplianceRate, lng) },
            { key: 'median', label: t('askTeacher.median'), value: formatElapsed(data.medianReplySeconds, lng) },
          ]}
        />
      )}
    </MetricCard>
  );
}
