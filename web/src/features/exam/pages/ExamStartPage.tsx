import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetUnitExamOverview } from '@/shared/api/generated/exams/exams';
import { ExamBlueprintSummary } from '../components/ExamBlueprintSummary';
import { ExamStartActions } from '../components/ExamStartActions';

export interface ExamStartPageProps {
  unitId: string;
}

export function ExamStartPage({ unitId }: ExamStartPageProps) {
  const { t } = useTranslation('exam');
  const { data, error, isPending, isError, refetch } = useGetUnitExamOverview(unitId);

  if (isError) {
    return (
      <ContentErrorState
        title={t('start.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  if (isPending) {
    return <ContentListSkeleton label={t('start.loading')} />;
  }
  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('start.title', { unit: data.unitName })}</h1>
      {data.blueprint ? (
        <ExamBlueprintSummary blueprint={data.blueprint} />
      ) : (
        <div className="rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
          <p className="text-ui text-text-muted">{t('start.noBlueprint')}</p>
        </div>
      )}
      <ExamStartActions overview={data} />
    </section>
  );
}
