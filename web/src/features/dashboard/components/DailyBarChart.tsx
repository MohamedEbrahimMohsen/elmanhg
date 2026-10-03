import { useTranslation } from 'react-i18next';
import { axisTickIndices, niceCeiling, type DailyPoint } from '../api/dailySeries';
import { formatDay } from '../api/metricFormat';
import { DailyBarPlot } from './DailyBarPlot';

export interface DailyBarChartProps {
  label: string;
  points: readonly DailyPoint[];
  formatValue: (value: number) => string;
  formatTick: (value: number) => string;
  emptyText: string;
  note?: string | undefined;
}

export function DailyBarChart({ label, points, formatValue, formatTick, emptyText, note }: DailyBarChartProps) {
  const { t, i18n } = useTranslation('dashboard');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const peak = Math.max(0, ...points.map((point) => point.value));
  const noteLine = note ? <p className="text-caption text-text-muted">{note}</p> : null;

  if (peak === 0) {
    return (
      <>
        {noteLine}
        <p className="flex h-40 items-center justify-center text-caption text-text-muted">{emptyText}</p>
      </>
    );
  }

  const max = niceCeiling(peak);
  const sum = points.reduce((total, point) => total + point.value, 0);
  const ticks = new Set(axisTickIndices(points.length));

  return (
    <div className="flex flex-col gap-2">
      {noteLine}
      <p className="text-caption text-text-muted">
        {t('charts.summary', { total: formatValue(sum), peak: formatValue(peak) })}
      </p>
      <div dir="ltr" aria-hidden="true" className="flex gap-2">
        <div className="flex h-32 flex-col items-end justify-between text-micro text-text-muted">
          <span>{formatTick(max)}</span>
          <span>{formatTick(max / 2)}</span>
          <span>{formatTick(0)}</span>
        </div>
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <DailyBarPlot
            points={points}
            max={max}
            describe={(point) =>
              t('charts.point', { date: formatDay(point.date, lng), value: formatValue(point.value) })
            }
          />
          <div className="flex">
            {points.map((point, index) => (
              <span
                key={point.date}
                className="flex min-w-0 flex-1 justify-center text-micro whitespace-nowrap text-text-muted"
              >
                {ticks.has(index) ? formatDay(point.date, lng) : null}
              </span>
            ))}
          </div>
        </div>
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
