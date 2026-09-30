import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentProgress } from '@/shared/api/generated/students/students';
import { StudentSubjectCard } from './StudentSubjectCard';
import { StudentWeakSpots } from './StudentWeakSpots';

export interface StudentProgressSectionProps {
  studentId: string;
}

export function StudentProgressSection({ studentId }: StudentProgressSectionProps) {
  const { t } = useTranslation('users');
  const headingId = useId();
  const { data, error, isPending, isError, refetch } = useGetStudentProgress(studentId);

  const renderSubjects = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('progress.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('progress.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.subjects.length === 0) {
      return <p className="text-ui text-text-muted">{t('progress.empty')}</p>;
    }
    return (
      <ul className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {data.subjects.map((subject) => (
          <li key={subject.subjectId}>
            <StudentSubjectCard subject={subject} />
          </li>
        ))}
      </ul>
    );
  };

  return (
    <>
      <section aria-labelledby={headingId} className="flex flex-col gap-3">
        <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
          {t('progress.title')}
        </h2>
        {renderSubjects()}
      </section>
      {data ? <StudentWeakSpots weakSpots={data.weakSpots} /> : null}
    </>
  );
}
