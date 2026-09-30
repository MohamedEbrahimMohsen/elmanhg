import { useTranslation } from 'react-i18next';
import { useGetDashboardAskTeacher } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardAskTeacherParams } from '@/shared/api/generated/model';
import { formatCount, formatElapsed, formatRate } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface AskTeacherCardProps {
  params: GetDashboardAskTeacherParams;
}

export function AskTeacherCard({ params }: AskTeacherCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardAskTeacher(params);

  return (
    <MetricCard title={t('askTeacher.title')} query={query}>
      {(data) => (
        <KpiFigure value={formatCount(data.openThreads, lng)} caption={t('askTeacher.caption')}>
          <li>{t('askTeacher.awaiting', { count: formatCount(data.awaitingReply, lng) })}</li>
          <li className={Number(data.overdueNow) > 0 ? 'font-semibold text-danger' : undefined}>
            {t('askTeacher.overdue', { count: formatCount(data.overdueNow, lng) })}
          </li>
          <li>{t('askTeacher.breaches', { count: formatCount(data.slaBreaches, lng) })}</li>
          <li>
            {t('askTeacher.replies', {
              count: formatCount(data.replies, lng),
              within: formatCount(data.repliedWithinSla, lng),
            })}
          </li>
          <li>{t('askTeacher.compliance', { rate: formatRate(data.slaComplianceRate, lng) })}</li>
          <li>{t('askTeacher.median', { duration: formatElapsed(data.medianReplySeconds, lng) })}</li>
        </KpiFigure>
      )}
    </MetricCard>
  );
}
