import { useTranslation } from 'react-i18next';
import {
  useGetDashboardAskTeacher,
  useGetDashboardSolveRate,
  useGetDashboardSuccessRate,
  useGetDashboardValidation,
} from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardSolveRateParams } from '@/shared/api/generated/model';
import { formatCount, formatRate, formatRatio } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface LearningTilesProps {
  params: GetDashboardSolveRateParams;
}

export function LearningTiles({ params }: LearningTilesProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const successRate = useGetDashboardSuccessRate(params);
  const solveRate = useGetDashboardSolveRate(params);
  const validation = useGetDashboardValidation(params);
  const askTeacher = useGetDashboardAskTeacher(params);
  const count = (value: number | string) => formatCount(value, lng);

  return (
    <>
      <MetricCard title={t('tiles.successRate')} query={successRate}>
        {(data) => (
          <KpiFigure
            value={formatRate(data.rate, lng)}
            caption={t('tiles.successRateDetail', { correct: count(data.correct), attempts: count(data.attempts) })}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.solveRate')} query={solveRate}>
        {(data) => (
          <KpiFigure
            value={formatRatio(data.attemptsPerActiveStudentPerDay, lng)}
            caption={t('tiles.solveRateDetail', {
              attempts: count(data.attempts),
              studentDays: count(data.activeStudentDays),
            })}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.validation')} query={validation}>
        {(data) => (
          <KpiFigure
            value={count(data.pendingBacklog)}
            caption={t('tiles.validationDetail', { approved: count(data.approved), rejected: count(data.rejected) })}
          />
        )}
      </MetricCard>
      <MetricCard title={t('tiles.askTeacher')} query={askTeacher}>
        {(data) => (
          <KpiFigure
            value={count(data.openThreads)}
            caption={t('tiles.askTeacherDetail', { count: count(data.overdueNow) })}
          />
        )}
      </MetricCard>
    </>
  );
}
