import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetStudentSubject } from '@/shared/api/generated/browse/browse';
import { Button } from '@/shared/ui/button';
import { MasterySummary } from '../components/MasterySummary';
import { StudentBreadcrumbs } from '../components/StudentBreadcrumbs';
import { UnitListItem } from '../components/UnitListItem';

export interface SubjectPageProps {
  subjectId: string;
}

export function SubjectPage({ subjectId }: SubjectPageProps) {
  const { t } = useTranslation('browse');
  const { data, error, isPending, isError, refetch } = useGetStudentSubject(subjectId);

  if (isError) {
    return (
      <ContentErrorState
        title={t('subject.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('subject.loading')} />;
  }
  return (
    <section className="flex flex-col gap-4">
      <StudentBreadcrumbs current={data.name} />
      <h1 className="font-display text-h1 font-bold break-words lg:text-h1-desktop">{data.name}</h1>
      <MasterySummary
        name={data.name}
        percent={Number(data.masteryPercent)}
        servable={Number(data.servableCount)}
        seen={Number(data.seenCount)}
      />
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('subject.unitsTitle')}</h2>
      {data.units.length === 0 ? (
        <p className="text-ui text-text-muted">{t('subject.empty')}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {data.units.map((unit) => (
            <UnitListItem key={unit.id} unit={unit} />
          ))}
        </ul>
      )}
      <Button asChild variant="secondary" className="self-start">
        <Link to="/student/multi-exam" search={{ subjectId: data.id }}>
          {t('subject.multiExam')}
        </Link>
      </Button>
    </section>
  );
}
