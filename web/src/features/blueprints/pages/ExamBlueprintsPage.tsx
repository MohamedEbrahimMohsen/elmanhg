import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { BlueprintsEmptyState } from '../components/BlueprintsEmptyState';
import { SubjectBlueprints } from '../components/SubjectBlueprints';
import { SubjectPicker } from '../components/SubjectPicker';
import { useBlueprintsSearch } from '../hooks/useBlueprintsSearch';
import { registerBlueprintsLocales } from '../locales';

registerBlueprintsLocales();

export function ExamBlueprintsPage() {
  const { t } = useTranslation('blueprints');
  const { data: subjects, error, isPending, isError, refetch } = useGetSubjects();
  const { subjectId, selectSubject } = useBlueprintsSearch();

  const renderBody = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('page.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('page.subjectsErrorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    const selected = subjects.find((subject) => subject.id === subjectId) ?? subjects[0];
    if (!selected) {
      return <BlueprintsEmptyState />;
    }
    return (
      <>
        <SubjectPicker subjects={subjects} value={selected.id} onChange={selectSubject} />
        <SubjectBlueprints key={selected.id} subjectId={selected.id} />
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-caption text-text-muted">{t('page.intro')}</p>
      {renderBody()}
    </section>
  );
}
