import { useTranslation } from 'react-i18next';
import type { ValidationQueueItemResult } from '@/shared/api/generated/model';
import { ValidationQueueItem } from './ValidationQueueItem';

export interface ValidationQueueListProps {
  items: ValidationQueueItemResult[];
  selectedIds: ReadonlySet<string>;
  onToggle: (id: string) => void;
}

export function ValidationQueueList({ items, selectedIds, onToggle }: ValidationQueueListProps) {
  const { t } = useTranslation('questions');
  const now = new Date();

  return (
    <ul aria-label={t('validation.queue.listLabel')} className="flex flex-col gap-2">
      {items.map((item) => (
        <ValidationQueueItem
          key={item.id}
          item={item}
          selected={selectedIds.has(item.id)}
          onToggle={onToggle}
          now={now}
        />
      ))}
    </ul>
  );
}
