import { FileDown } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export function TrainingExportEmptyState() {
  const { t } = useTranslation('trainingExport');

  return (
    <div className="flex flex-col items-center gap-3 p-6 text-center">
      <FileDown aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{t('list.empty')}</p>
    </div>
  );
}
