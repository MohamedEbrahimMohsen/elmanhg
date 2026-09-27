import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';

export interface OutcomeBadgeProps {
  outcome: string;
}

const outcomeClasses: Partial<Record<string, string>> = {
  Success: 'bg-success text-surface',
  Failure: 'bg-danger text-surface',
};

export function OutcomeBadge({ outcome }: OutcomeBadgeProps) {
  const { t } = useTranslation('audit');

  return (
    <span
      className={cn(
        'inline-flex rounded-pill px-2.5 py-0.5 text-micro font-semibold',
        outcomeClasses[outcome] ?? 'bg-soft text-text-muted',
      )}
    >
      {t([`outcome.${outcome}`, outcome])}
    </span>
  );
}
