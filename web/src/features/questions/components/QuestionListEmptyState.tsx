import { ListChecks } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface QuestionListEmptyStateProps {
  variant: 'no-data' | 'no-results';
  onClear: () => void;
}

export function QuestionListEmptyState({ variant, onClear }: QuestionListEmptyStateProps) {
  const { t } = useTranslation('questions');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <ListChecks aria-hidden className="size-8 text-text-muted" />
      <p className="text-ui text-text">{t(variant === 'no-data' ? 'list.empty.noData' : 'list.empty.noResults')}</p>
      {variant === 'no-results' ? (
        <Button variant="secondary" onClick={onClear}>
          {t('list.filters.clear')}
        </Button>
      ) : null}
    </div>
  );
}
