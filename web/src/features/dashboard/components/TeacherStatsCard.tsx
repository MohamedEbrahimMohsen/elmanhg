import { useTranslation } from 'react-i18next';
import { useGetMyTeacherStats } from '@/shared/api/generated/dashboard/dashboard';
import { formatCount, formatDay, formatElapsed, formatRate } from '../api/metricFormat';
import { registerTeacherStatsLocales } from '../teacherStatsLocales';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

registerTeacherStatsLocales();

export function TeacherStatsCard() {
  const { t, i18n } = useTranslation('teacherStats');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetMyTeacherStats();

  return (
    <MetricCard ns="teacherStats" title={t('card.title')} query={query}>
      {(data) => {
        const decisions = Number(data.approved) + Number(data.rejected);
        const idle = decisions === 0 && Number(data.replies) === 0;
        return (
          <KpiFigure
            value={formatCount(decisions, lng)}
            caption={t('card.caption', { from: formatDay(data.from, lng), to: formatDay(data.to, lng) })}
            note={idle ? t('card.empty') : undefined}
          >
            <li>{t('card.approved', { count: formatCount(data.approved, lng) })}</li>
            <li>{t('card.rejected', { count: formatCount(data.rejected, lng) })}</li>
            <li>{t('card.medianDecision', { duration: formatElapsed(data.medianSecondsToDecision, lng) })}</li>
            <li>{t('card.compliance', { rate: formatRate(data.slaComplianceRate, lng) })}</li>
            <li>
              {t('card.replies', {
                count: formatCount(data.replies, lng),
                within: formatCount(data.repliedWithinSla, lng),
              })}
            </li>
            <li>{t('card.medianReply', { duration: formatElapsed(data.medianReplySeconds, lng) })}</li>
          </KpiFigure>
        );
      }}
    </MetricCard>
  );
}
