import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useSession } from '@/features/session';
import { useGetMasteryOverview } from '@/shared/api/generated/mastery/mastery';
import { HeadlineCounterCard } from '../components/HeadlineCounterCard';
import { NextLessonCard } from '../components/NextLessonCard';
import { SubjectMasteryCard } from '../components/SubjectMasteryCard';

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
      {data.nextLesson ? <NextLessonCard lesson={data.nextLesson} /> : null}
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('subjects.title')}</h2>
      {data.subjects.length === 0 ? (
        <p className="text-ui text-text-muted">{t('subjects.empty')}</p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
          {data.subjects.map((subject) => (
            <li key={subject.subjectId}>
              <SubjectMasteryCard subject={subject} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
