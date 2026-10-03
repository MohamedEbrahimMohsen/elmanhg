import { useId } from 'react';
import { ShareBar } from './ShareBar';

export interface BarListItem {
  key: string;
  label: string;
  value: number;
  display: string;
}

export function BarList({ items, max }: { items: readonly BarListItem[]; max: number }) {
  const id = useId();

  return (
    <ul className="flex flex-col gap-3">
      {items.map(({ key, label, value, display }) => (
        <li key={key} className="flex flex-col gap-1">
          <div className="flex items-baseline justify-between gap-3 text-caption">
            <span id={`${id}-${key}-l`} className="text-text">
              {label}
            </span>
            <span id={`${id}-${key}-v`} className="font-semibold text-text">
              {display}
            </span>
          </div>
          <ShareBar percent={max > 0 ? (value / max) * 100 : 0} aria-labelledby={`${id}-${key}-l ${id}-${key}-v`} />
        </li>
      ))}
    </ul>
  );
}
