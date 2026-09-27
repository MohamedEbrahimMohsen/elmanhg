import { ScrollText } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface AuditLogEmptyStateProps {
  variant: 'no-data' | 'no-results';
  onClear: () => void;
}

export function AuditLogEmptyState({ variant, onClear }: AuditLogEmptyStateProps) {
  const { t } = useTranslation('audit');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <ScrollText aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{t(variant === 'no-data' ? 'empty.noData' : 'empty.noResults')}</p>
      {variant === 'no-results' ? (
        <Button variant="primary" onClick={onClear}>
          {t('filters.clear')}
        </Button>
      ) : null}
    </div>
  );
}
