import { Check, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import type { DiagramItemMark } from '../api/diagramReview';
import type { DiagramChipHandlers } from '../hooks/useDiagramDrag';

export interface DiagramItemChipProps {
  item: { id: string; text: string };
  selected: boolean;
  interactive: boolean;
  mark?: DiagramItemMark | undefined;
  handlers?: DiagramChipHandlers | undefined;
  onSelect?: ((itemId: string) => void) | undefined;
  chipRef?: ((element: HTMLButtonElement | null) => void) | undefined;
}

const chipClassName = 'inline-flex min-h-11 items-center gap-1.5 rounded-full border px-3.5 text-ui text-text';

const markClassNames = {
  correct: 'border-success bg-success-soft',
  wrong: 'border-danger bg-danger-soft',
  missed: 'border-danger bg-danger-soft',
} as const;

export function DiagramItemChip({
  item,
  selected,
  interactive,
  mark,
  handlers,
  onSelect,
  chipRef,
}: DiagramItemChipProps) {
  const { t } = useTranslation('diagramStudent');

  if (interactive) {
    return (
      <button
        ref={chipRef}
        type="button"
        aria-pressed={selected}
        data-item-id={item.id}
        onClick={() => onSelect?.(item.id)}
        className={cn(
          chipClassName,
          'touch-none select-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden',
          selected ? 'border-text bg-soft' : 'border-border-strong bg-surface',
        )}
        {...handlers}
      >
        <bdi>{item.text}</bdi>
      </button>
    );
  }

  const Icon = mark === 'correct' ? Check : X;
  return (
    <span className={cn(chipClassName, mark ? markClassNames[mark] : 'border-border-strong bg-surface')}>
      {mark ? (
        <Icon aria-hidden className={cn('size-4 shrink-0', mark === 'correct' ? 'text-success' : 'text-danger')} />
      ) : null}
      <bdi>{item.text}</bdi>
      {mark ? <span className="sr-only"> {t(`mark.${mark}`)}</span> : null}
    </span>
  );
}
