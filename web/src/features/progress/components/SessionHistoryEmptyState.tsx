import { History } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface SessionHistoryEmptyStateProps {
  variant: 'no-data' | 'no-results';
  onClear: () => void;
}

export function SessionHistoryEmptyState({ variant, onClear }: SessionHistoryEmptyStateProps) {
  const { t } = useTranslation('progress');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <History aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">
        {t(variant === 'no-data' ? 'history.empty.noData' : 'history.empty.noResults')}
      </p>
      {variant === 'no-results' ? (
        <Button variant="primary" onClick={onClear}>
          {t('history.empty.clear')}
        </Button>
      ) : null}
    </div>
  );
}
