import { useId } from 'react';

export interface MetricListRow {
  key: string;
  label: string;
  value: string;
  tone?: 'danger' | undefined;
}

export function MetricList({ rows }: { rows: readonly MetricListRow[] }) {
  const id = useId();

  return (
    <dl className="flex flex-col divide-y divide-border text-caption">
      {rows.map(({ key, label, value, tone }) => (
        <div key={key} className="flex items-baseline justify-between gap-3 py-2">
          <dt id={`${id}-${key}`} className="text-text-muted">
            {label}
          </dt>
          <dd
            aria-labelledby={`${id}-${key}`}
            className={tone === 'danger' ? 'font-bold text-danger' : 'font-bold text-text'}
          >
            {value}
          </dd>
        </div>
      ))}
    </dl>
  );
}
