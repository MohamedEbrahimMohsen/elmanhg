import { useTranslation } from 'react-i18next';
import { useGetDashboardContent } from '@/shared/api/generated/dashboard/dashboard';
import type { GetDashboardContentParams } from '@/shared/api/generated/model';
import { formatCount } from '../api/metricFormat';
import { KpiFigure } from './KpiFigure';
import { MetricCard } from './MetricCard';

export interface ContentCardProps {
  params: GetDashboardContentParams;
}

export function ContentCard({ params }: ContentCardProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const query = useGetDashboardContent(params);
  const count = (value: number | string) => formatCount(value, lng);

  return (
    <MetricCard title={t('content.title')} query={query}>
      {(data) => (
        <>
          <KpiFigure value={count(data.servableTotal)} caption={t('content.caption')} note={t('card.snapshot')}>
            <li>{t('content.structure', { subjects: count(data.subjects), units: count(data.units) })}</li>
            <li>
              {t('content.lessons', {
                published: count(data.lessonsPublished),
                draft: count(data.lessonsDraft),
                archived: count(data.lessonsArchived),
              })}
            </li>
            <li>
              {t('content.questions', {
                pending: count(data.questionsPending),
                approved: count(data.questionsApproved),
                rejected: count(data.questionsRejected),
                retired: count(data.questionsRetired),
              })}
            </li>
          </KpiFigure>
          <h3 className="text-caption font-semibold text-text-muted">{t('content.byType')}</h3>
          <ul className="flex flex-col gap-1 text-caption text-text">
            {data.questionsByType.map((entry) => (
              <li key={entry.type}>
                {t('content.typeCount', { type: t(`questions:types.${entry.type}`), count: count(entry.count) })}
              </li>
            ))}
          </ul>
        </>
      )}
    </MetricCard>
  );
}
