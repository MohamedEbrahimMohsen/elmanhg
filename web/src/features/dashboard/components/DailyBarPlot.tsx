import type { DailyPoint } from '../api/dailySeries';

export interface DailyBarPlotProps {
  points: readonly DailyPoint[];
  max: number;
  describe: (point: DailyPoint) => string;
}

// viewBox units, not CSS px. The three y labels are text-micro lines (17 px) spread over the
// h-32 (128 px) plot, so their centres sit at 6.64 and 93.36 of 100: the gridlines meet them there.
const slotWidth = 10;
const barWidth = 6;
const plotTop = 6.64;
const plotBase = 93.36;
const plotHeight = plotBase - plotTop;

export function DailyBarPlot({ points, max, describe }: DailyBarPlotProps) {
  const width = points.length * slotWidth;
  const middle = plotTop + plotHeight / 2;

  return (
    <svg viewBox={`0 0 ${String(width)} 100`} preserveAspectRatio="none" className="h-32 w-full">
      {[plotTop, middle].map((y) => (
        <line key={y} x1={0} y1={y} x2={width} y2={y} className="stroke-border" vectorEffect="non-scaling-stroke" />
      ))}
      {points.map((point, index) => {
        const centre = index * slotWidth + slotWidth / 2;
        const height = point.value > 0 ? Math.max(1, (point.value / max) * plotHeight) : 0;
        return (
          <g key={point.date}>
            <line
              x1={centre}
              y1={plotBase}
              x2={centre}
              y2={100}
              className="stroke-border-strong"
              vectorEffect="non-scaling-stroke"
            />
            <rect
              x={centre - barWidth / 2}
              y={plotBase - height}
              width={barWidth}
              height={height}
              className="fill-accent"
            />
            <rect x={index * slotWidth} y={0} width={slotWidth} height={100} fill="none" pointerEvents="all">
              <title>{describe(point)}</title>
            </rect>
          </g>
        );
      })}
      <line
        x1={0}
        y1={plotBase}
        x2={width}
        y2={plotBase}
        className="stroke-border-strong"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
