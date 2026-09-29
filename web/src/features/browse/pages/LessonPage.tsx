import { Outlet } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentLesson } from '@/shared/api/generated/browse/browse';
import { LessonNavigation } from '../components/LessonNavigation';
import { LessonTabs } from '../components/LessonTabs';
import { LockedLessonNotice } from '../components/LockedLessonNotice';
import { MasterySummary } from '../components/MasterySummary';
import { StudentBreadcrumbs } from '../components/StudentBreadcrumbs';
import { useLessonOpening } from '../hooks/useLessonOpening';

export interface LessonPageProps {
  lessonId: string;
}

export function LessonPage({ lessonId }: LessonPageProps) {
  const { t } = useTranslation('browse');
  const query = useGetStudentLesson(lessonId);
  useLessonOpening(lessonId, query.data?.isLocked ? undefined : query.data?.unitId);
  const { data, error, isPending, isError, refetch } = query;

  if (isError) {
    return (
      <ContentErrorState
        title={t('lesson.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('lesson.loading')} />;
  }
  return (
    <section className="flex flex-col gap-4">
      <StudentBreadcrumbs
        subject={{ id: data.subjectId, name: data.subjectName }}
        unit={{ id: data.unitId, name: data.unitName }}
        current={data.name}
      />
      <h1 className="font-display text-h1 font-bold break-words lg:text-h1-desktop">{data.name}</h1>
      <MasterySummary
        name={data.name}
        percent={Number(data.masteryPercent)}
        servable={Number(data.servableCount)}
        seen={Number(data.seenCount)}
      />
      {data.isLocked ? (
        <LockedLessonNotice />
      ) : (
        <>
          <LessonTabs lessonId={lessonId} />
          <Outlet />
        </>
      )}
      <LessonNavigation lesson={data} />
    </section>
  );
}
