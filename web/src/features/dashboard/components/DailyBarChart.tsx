import { useTranslation } from 'react-i18next';
import { formatDay } from '../api/metricFormat';

export interface DailyPoint {
  date: string;
  value: number;
}

export interface DailyBarChartProps {
  label: string;
  points: readonly DailyPoint[];
  formatValue: (value: number) => string;
  emptyText: string;
  note?: string | undefined;
}

// viewBox units, not CSS px
const slotWidth = 10;
const barWidth = 8;
const chartHeight = 100;

export function DailyBarChart({ label, points, formatValue, emptyText, note }: DailyBarChartProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const peak = Math.max(0, ...points.map((point) => point.value));
  const first = points.at(0);
  const last = points.at(-1);
  const noteLine = note ? <p className="text-caption text-text-muted">{note}</p> : null;

  if (peak === 0 || !first || !last) {
    return (
      <>
        {noteLine}
        <p className="text-caption text-text-muted">{emptyText}</p>
      </>
    );
  }

  const width = points.length * slotWidth;

  return (
    <div className="flex flex-col gap-2">
      {noteLine}
      <svg
        aria-hidden="true"
        viewBox={`0 0 ${String(width)} ${String(chartHeight)}`}
        preserveAspectRatio="none"
        className="h-32 w-full"
      >
        {points.map((point, index) => {
          const height = (point.value / peak) * chartHeight;
          return (
            <rect
              key={point.date}
              x={index * slotWidth + 1}
              y={chartHeight - height}
              width={barWidth}
              height={height}
              className="fill-text-muted"
            >
              <title>{`${formatDay(point.date, lng)}: ${formatValue(point.value)}`}</title>
            </rect>
          );
        })}
        <line
          x1={0}
          y1={chartHeight}
          x2={width}
          y2={chartHeight}
          className="stroke-border-strong"
          vectorEffect="non-scaling-stroke"
        />
      </svg>
      <div dir="ltr" className="flex justify-between text-micro text-text-muted">
        <span dir="auto">{formatDay(first.date, lng)}</span>
        <span dir="auto">{t('charts.peak', { value: formatValue(peak) })}</span>
        <span dir="auto">{formatDay(last.date, lng)}</span>
      </div>
      <table className="sr-only">
        <caption>{label}</caption>
        <thead>
          <tr>
            <th scope="col">{t('charts.date')}</th>
            <th scope="col">{t('charts.value')}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((point) => (
            <tr key={point.date}>
              <td>{formatDay(point.date, lng)}</td>
              <td>{formatValue(point.value)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
