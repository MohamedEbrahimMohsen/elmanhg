import { useTranslation } from 'react-i18next';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { Pagination } from '@/shared/components/Pagination';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { TrainingExportEmptyState } from '../components/TrainingExportEmptyState';
import { TrainingExportForm } from '../components/TrainingExportForm';
import { TrainingExportSkeleton } from '../components/TrainingExportSkeleton';
import { TrainingExportTable } from '../components/TrainingExportTable';
import { useTrainingExports } from '../hooks/useTrainingExports';
import { useTrainingExportSearch } from '../hooks/useTrainingExportSearch';

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1';

export function TrainingExportPage() {
  const { t } = useTranslation('trainingExport');
  const { search, setPage } = useTrainingExportSearch();
  const { data, error, isPending, isError, refetch } = useTrainingExports(search);
  const { data: subjects = [] } = useGetSubjects();
  const errorCode = error instanceof ApiError ? error.code : unhandledErrorCode;

  const renderList = () => {
    if (isPending) {
      return <TrainingExportSkeleton />;
    }
    if (isError) {
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-semibold text-danger">{t('list.errorTitle')}</p>
          <p className="text-caption text-text">
            {t([`common:errors.${errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              void refetch();
            }}
          >
            {t('common:actions.retry')}
          </Button>
        </div>
      );
    }
    if (data.items.length === 0) {
      return <TrainingExportEmptyState />;
    }
    return (
      <>
        <TrainingExportTable items={data.items} subjects={subjects} />
        {data.totalPages > 1 ? (
          <Pagination page={data.pageNumber} totalPages={data.totalPages} onPageChange={setPage} />
        ) : null}
      </>
    );
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <div className={cardClassName}>
        <p className="text-ui text-text">{t('page.intro')}</p>
        <p className="text-caption text-text-muted">{t('page.note')}</p>
        <TrainingExportForm subjects={subjects} />
      </div>
      <div className={cardClassName}>
        <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('list.title')}</h2>
        {renderList()}
      </div>
    </section>
  );
}
