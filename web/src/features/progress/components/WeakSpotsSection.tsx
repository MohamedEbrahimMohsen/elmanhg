import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetWeakSpots } from '@/shared/api/generated/progress/progress';
import { WeakLessonList } from './WeakLessonList';
import { WeakObjectiveList } from './WeakObjectiveList';

const subHeading = 'font-display text-h3 font-semibold';

export function WeakSpotsSection() {
  const { t } = useTranslation('progress');
  const headingId = useId();
  const { data, error, isPending, isError, refetch } = useGetWeakSpots();

  const renderContent = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('weakSpots.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('weakSpots.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.lessons.length === 0 && data.objectives.length === 0) {
      return <p className="text-ui text-text-muted">{t('weakSpots.empty')}</p>;
    }
    return (
      <>
        {data.lessons.length > 0 ? (
          <>
            <h3 className={subHeading}>{t('weakSpots.lessons')}</h3>
            <WeakLessonList lessons={data.lessons} />
          </>
        ) : null}
        {data.objectives.length > 0 ? (
          <>
            <h3 className={subHeading}>{t('weakSpots.objectives')}</h3>
            <WeakObjectiveList objectives={data.objectives} />
          </>
        ) : null}
      </>
    );
  };

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('weakSpots.title')}
      </h2>
      {renderContent()}
    </section>
  );
}
