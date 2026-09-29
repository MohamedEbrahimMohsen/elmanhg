import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentUnit } from '@/shared/api/generated/browse/browse';
import { LessonListItem } from '../components/LessonListItem';
import { MasterySummary } from '../components/MasterySummary';
import { StudentBreadcrumbs } from '../components/StudentBreadcrumbs';
import { UnitExamCard } from '../components/UnitExamCard';

export interface UnitPageProps {
  unitId: string;
}

export function UnitPage({ unitId }: UnitPageProps) {
  const { t } = useTranslation('browse');
  const { data, error, isPending, isError, refetch } = useGetStudentUnit(unitId);

  if (isError) {
    return (
      <ContentErrorState
        title={t('unit.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('unit.loading')} />;
  }
  return (
    <section className="flex flex-col gap-4">
      <StudentBreadcrumbs subject={{ id: data.subjectId, name: data.subjectName }} current={data.name} />
      <h1 className="font-display text-h1 font-bold break-words lg:text-h1-desktop">{data.name}</h1>
      <MasterySummary
        name={data.name}
        percent={Number(data.masteryPercent)}
        servable={Number(data.servableCount)}
        seen={Number(data.seenCount)}
      />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('unit.lessonsTitle')}</h2>
      {data.lessons.length === 0 ? (
        <p className="text-ui text-text-muted">{t('unit.empty')}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {data.lessons.map((lesson) => (
            <LessonListItem key={lesson.id} lesson={lesson} />
          ))}
        </ul>
      )}
      <UnitExamCard unitId={data.id} bestExamScorePercent={data.bestExamScorePercent} />
    </section>
  );
}
