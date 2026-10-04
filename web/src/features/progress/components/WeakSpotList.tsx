import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { WeakSpotRow, type WeakSpotRowProps } from './WeakSpotRow';

export interface WeakSpotItem extends WeakSpotRowProps {
  id: string;
}

export interface WeakSpotListProps {
  items: WeakSpotItem[];
}

const weakListCap = 3;

export function WeakSpotList({ items }: WeakSpotListProps) {
  const { t } = useTranslation('progress');
  const listId = useId();
  const [expanded, setExpanded] = useState(false);
  const shown = expanded ? items : items.slice(0, weakListCap);

  return (
    <div className="flex flex-col gap-2">
      <ul
        id={listId}
        className="flex flex-col divide-y divide-border rounded-lg border border-border bg-surface px-4 shadow-1 lg:px-5"
      >
        {shown.map(({ id, ...row }) => (
          <WeakSpotRow key={id} {...row} />
        ))}
      </ul>
      {items.length > weakListCap ? (
        <Button
          variant="ghost"
          className="self-start"
          aria-expanded={expanded}
          aria-controls={listId}
          onClick={() => {
            setExpanded(!expanded);
          }}
        >
          {expanded ? t('weakSpots.showLess') : t('weakSpots.showAll', { count: items.length })}
        </Button>
      ) : null}
    </div>
  );
}
