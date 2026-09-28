import { useTranslation } from 'react-i18next';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { ContentEmptyState } from '../components/ContentEmptyState';
import { ContentErrorState } from '../components/ContentErrorState';
import { ContentListSkeleton } from '../components/ContentListSkeleton';
import { NameForm } from '../components/NameForm';
import { SubjectCard } from '../components/SubjectCard';
import { useSubjectMutations } from '../hooks/useSubjectMutations';

const subjectNameErrorFields = { SUBJECT_NAME_REQUIRED: 'name', SUBJECT_NAME_TOO_LONG: 'name' } as const;

export function ContentPage() {
  const { t } = useTranslation('content');
  const { data, error, isPending, isError, refetch } = useGetSubjects();
  const { create } = useSubjectMutations();

  const renderSubjects = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('page.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('error.title')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.length === 0) {
      return <ContentEmptyState message={t('subjects.empty')} />;
    }
    return (
      <ol aria-label={t('subjects.listLabel')} className="flex flex-col gap-3">
        {data.map((subject, index) => (
          <SubjectCard
            key={subject.id}
            subject={subject}
            isFirst={index === 0}
            isLast={index === data.length - 1}
            position={index + 1}
          />
        ))}
      </ol>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <div className="rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
        <NameForm
          label={t('subjects.nameLabel')}
          submitLabel={t('subjects.add')}
          serverErrorFields={subjectNameErrorFields}
          onSubmit={create}
        />
      </div>
      {renderSubjects()}
    </section>
  );
}
