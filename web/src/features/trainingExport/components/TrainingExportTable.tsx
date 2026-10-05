import { useTranslation } from 'react-i18next';
import type { SubjectResult, TrainingExportResult } from '@/shared/api/generated/model';
import { useDownloadTrainingExport } from '../hooks/useDownloadTrainingExport';
import { TrainingExportRow } from './TrainingExportRow';

export interface TrainingExportTableProps {
  items: TrainingExportResult[];
  subjects: SubjectResult[];
}

const headerKeys = ['source', 'period', 'subject', 'status', 'rows', 'size', 'requestedAt', 'action'] as const;

export function TrainingExportTable({ items, subjects }: TrainingExportTableProps) {
  const { t } = useTranslation('trainingExport');
  const { download, pendingId } = useDownloadTrainingExport();

  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse">
        <caption className="sr-only">{t('list.title')}</caption>
        <thead>
          <tr>
            {headerKeys.map((key) => (
              <th key={key} scope="col" className="px-2.5 py-2.25 text-start text-caption font-bold text-text-muted">
                {t(`list.columns.${key}`)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <TrainingExportRow
              key={item.id}
              item={item}
              subjectName={subjects.find((subject) => subject.id === item.subjectId)?.name ?? t('form.allSubjects')}
              downloading={pendingId === item.id}
              onDownload={download}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
