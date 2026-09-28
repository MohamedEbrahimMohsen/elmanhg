import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjectProgress } from '@/shared/api/generated/progress/progress';
import { SubjectProgressCard } from './SubjectProgressCard';

export function SubjectProgressSection() {
  const { t } = useTranslation('progress');
  const headingId = useId();
  const { data, error, isPending, isError, refetch } = useGetSubjectProgress();

  const renderContent = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('subjects.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('subjects.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.length === 0) {
      return <p className="text-ui text-text-muted">{t('subjects.empty')}</p>;
    }
    return (
      <ul className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {data.map((subject) => (
          <li key={subject.subjectId}>
            <SubjectProgressCard subject={subject} />
          </li>
        ))}
      </ul>
    );
  };

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-3">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t('subjects.title')}
      </h2>
      {renderContent()}
    </section>
  );
}
