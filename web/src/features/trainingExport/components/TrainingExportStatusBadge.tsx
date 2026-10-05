import { useTranslation } from 'react-i18next';
import type { TrainingExportStatus } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';

export interface TrainingExportStatusBadgeProps {
  status: TrainingExportStatus;
}

const statusClasses: Record<TrainingExportStatus, string> = {
  Pending: 'bg-soft text-text-muted',
  Completed: 'bg-success-soft text-success-text',
  Failed: 'bg-danger-soft text-danger',
  Expired: 'bg-warning-soft text-text',
};

export function TrainingExportStatusBadge({ status }: TrainingExportStatusBadgeProps) {
  const { t } = useTranslation('trainingExport');

  return (
    <span className={cn('inline-flex rounded-pill px-2.5 py-0.5 text-micro font-bold', statusClasses[status])}>
      {t(`status.${status}`)}
    </span>
  );
}
