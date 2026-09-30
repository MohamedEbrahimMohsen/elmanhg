import { useId } from 'react';
import { ArrowDown, ArrowUp, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { cn } from '@/shared/lib/utils';
import type { DiagramItemMark } from '../api/diagramReview';
import { DiagramItemChip, type DiagramItemChipProps } from './DiagramItemChip';

type ChipProps = Omit<DiagramItemChipProps, 'item' | 'interactive' | 'mark'>;

export interface DiagramZoneRowProps {
  zone: { id: string; capacity: number };
  number: number;
  items: { id: string; text: string }[];
  selectedItem: { id: string; text: string } | null;
  full: boolean;
  interactive: boolean;
  over: boolean;
  marks?: Record<string, DiagramItemMark> | undefined;
  chipProps: (itemId: string) => ChipProps;
  onPlace: (zoneId: string) => void;
  onReturn: (itemId: string) => void;
  onMove: (zoneId: string, index: number, delta: -1 | 1) => void;
}

const itemActions = [
  { key: 'moveUp', Icon: ArrowUp, delta: -1 },
  { key: 'moveDown', Icon: ArrowDown, delta: 1 },
  { key: 'returnToBank', Icon: Undo2, delta: 0 },
] as const;

type ItemActionKey = (typeof itemActions)[number]['key'];

export function DiagramZoneRow({
  zone,
  number,
  items,
  selectedItem,
  full,
  interactive,
  over,
  marks,
  chipProps,
  onPlace,
  onReturn,
  onMove,
}: DiagramZoneRowProps) {
  const { t } = useTranslation('diagramStudent');
  const reorderable = interactive && items.length >= 2;
  const headingId = useId();
  const visible = (key: ItemActionKey, index: number) =>
    key === 'returnToBank' ? interactive : reorderable && (key === 'moveUp' ? index > 0 : index < items.length - 1);
  const run = (delta: -1 | 0 | 1, index: number, itemId: string) => {
    if (delta === 0) {
      onReturn(itemId);
    } else {
      onMove(zone.id, index, delta);
    }
  };

  return (
    <li
      aria-labelledby={headingId}
      data-zone-id={zone.id}
      className={cn('flex flex-col gap-2 rounded-md border border-border bg-surface p-3', over && 'border-accent')}
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p id={headingId} className="text-ui font-semibold">
          {t('zone', { number })}
        </p>
        <span className="text-caption text-text-muted">
          {t('zoneFill', { count: items.length, capacity: zone.capacity })}
        </span>
      </div>
      {interactive && selectedItem ? (
        full ? (
          <span className="text-caption text-text-muted">{t('zoneFull')}</span>
        ) : (
          <div>
            <Button
              variant="secondary"
              size="sm"
              aria-label={`${t('placeHere')} ${t('placeHereTarget', { item: selectedItem.text, number })}`}
              onClick={() => {
                onPlace(zone.id);
              }}
            >
              {t('placeHere')}
            </Button>
          </div>
        )
      ) : null}
      {items.length === 0 ? (
        <p className="text-caption text-text-muted">{t('zoneEmpty')}</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item, index) => (
            <li key={item.id} className="flex flex-wrap items-center gap-1">
              <DiagramItemChip item={item} interactive={interactive} mark={marks?.[item.id]} {...chipProps(item.id)} />
              {itemActions
                .filter(({ key }) => visible(key, index))
                .map(({ key, Icon, delta }) => (
                  <Button
                    key={key}
                    variant="ghost"
                    className="min-w-11 px-0"
                    aria-label={t(key, { item: item.text })}
                    onClick={() => {
                      run(delta, index, item.id);
                    }}
                  >
                    <Icon aria-hidden className={cn('size-4.5', delta === 0 && 'rtl:-scale-x-100')} />
                  </Button>
                ))}
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
