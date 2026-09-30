import { useTranslation } from 'react-i18next';

const skeletonRows = ['first', 'second', 'third'];

export function TrainingExportSkeleton() {
  const { t } = useTranslation('trainingExport');

  return (
    <div role="status" aria-busy="true" aria-label={t('page.loading')} className="flex flex-col gap-3">
      {skeletonRows.map((row) => (
        <div key={row} className="h-11 rounded-md bg-soft" />
      ))}
    </div>
  );
}
