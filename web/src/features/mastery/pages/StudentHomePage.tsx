import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useSession } from '@/features/session';
import { PlanSummaryLine } from '@/features/subscription';
import { useGetMasteryOverview } from '@/shared/api/generated/mastery/mastery';
import { HeadlineCounterCard } from '../components/HeadlineCounterCard';
import { HomeSubjects } from '../components/HomeSubjects';
import { NextLessonCard } from '../components/NextLessonCard';

export function StudentHomePage() {
  const { t } = useTranslation('mastery');
  const session = useSession();
  const { data, error, isPending, isError, refetch } = useGetMasteryOverview();
  const greeting = (
    <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">
      {t('home.greeting', { name: session?.displayName ?? '' })}
    </h1>
  );

  if (isError) {
    return (
      <section className="flex flex-col gap-4">
        {greeting}
        <ContentErrorState
          title={t('home.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      </section>
    );
  }
  if (isPending) {
    return (
      <section className="flex flex-col gap-4">
        {greeting}
        <ContentListSkeleton label={t('home.loading')} />
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-4">
      {greeting}
      <HeadlineCounterCard headline={data.headline} streakDays={Number(data.streakDays)} />
      <PlanSummaryLine />
      {data.nextLesson ? <NextLessonCard lesson={data.nextLesson} /> : null}
      <HomeSubjects subjects={data.subjects} />
    </section>
  );
}
