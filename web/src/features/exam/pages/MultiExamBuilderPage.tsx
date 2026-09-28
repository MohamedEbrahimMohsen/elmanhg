import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { MultiExamSubjectSection } from '../components/MultiExamSubjectSection';
import { MultiExamSubjectSelect } from '../components/MultiExamSubjectSelect';
import { useMultiExamSearch } from '../hooks/useMultiExamSearch';

export function MultiExamBuilderPage() {
  const { t } = useTranslation('exam');
  const { data: subjects, error, isPending, isError, refetch } = useGetSubjects();
  const { subjectId, selectSubject } = useMultiExamSearch();

  const renderBody = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('multi.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('multi.subjectsErrorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    const selected = subjects.find((subject) => subject.id === subjectId) ?? subjects[0];
    if (!selected) {
      return <p className="text-ui text-text-muted">{t('multi.noSubjects')}</p>;
    }
    return (
      <>
        <MultiExamSubjectSelect subjects={subjects} value={selected.id} onChange={selectSubject} />
        <MultiExamSubjectSection key={selected.id} subjectId={selected.id} />
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('multi.title')}</h1>
      {renderBody()}
    </section>
  );
}
