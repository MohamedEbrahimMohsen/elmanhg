import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';

export interface QuestionStatusBadgeProps {
  status: string;
}

const statusClasses: Partial<Record<string, string>> = {
  Approved: 'bg-success-soft text-success-text',
  Rejected: 'bg-danger-soft text-danger',
};

export function QuestionStatusBadge({ status }: QuestionStatusBadgeProps) {
  const { t } = useTranslation('questions');

  return (
    <span
      className={cn(
        'rounded-full px-2.5 py-0.5 text-micro font-bold',
        statusClasses[status] ?? 'bg-soft text-text-muted',
      )}
    >
      {t([`statuses.${status}`, 'statuses.Pending'])}
    </span>
  );
}
