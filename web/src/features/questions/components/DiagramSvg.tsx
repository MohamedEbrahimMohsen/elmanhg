import type { PointerEventHandler } from 'react';
import { cn } from '@/shared/lib/utils';
import type { DiagramModel, DiagramRect } from '../api/dragDropValues';

export interface DiagramSvgProps {
  image: DiagramModel['image'];
  zones: readonly (DiagramRect & { id: string })[];
  tone: 'edit' | 'student' | 'key';
  label: string;
  draft?: DiagramRect | null | undefined;
  interactive?: boolean | undefined;
  onPointerDown?: PointerEventHandler<SVGSVGElement> | undefined;
  onPointerMove?: PointerEventHandler<SVGSVGElement> | undefined;
  onPointerUp?: PointerEventHandler<SVGSVGElement> | undefined;
  onPointerLeave?: PointerEventHandler<SVGSVGElement> | undefined;
}

// viewBox units (image pixels), not CSS px
const labelScale = 0.025;
const dash = '6 4';

const toneClassNames = {
  edit: 'fill-accent/15 stroke-accent stroke-2',
  student: 'fill-surface/60 stroke-text-muted stroke-2',
  key: 'fill-success/20 stroke-success stroke-2',
} as const;

export function DiagramSvg({ image, zones, tone, label, draft, interactive, ...handlers }: DiagramSvgProps) {
  const { width: w, height: h } = image;
  const r = Math.max(w, h) * labelScale;
  const scale = (rect: DiagramRect) => ({
    x: (rect.x / 100) * w,
    y: (rect.y / 100) * h,
    width: (rect.width / 100) * w,
    height: (rect.height / 100) * h,
  });

  return (
    <svg
      role="img"
      aria-label={label}
      viewBox={`0 0 ${String(w)} ${String(h)}`}
      className={cn(
        'block h-auto w-full rounded-md border border-border bg-surface',
        interactive && 'cursor-crosshair touch-none select-none',
      )}
      {...handlers}
    >
      <image href={image.url} x={0} y={0} width={w} height={h} preserveAspectRatio="none" />
      {zones.map((zone, index) => {
        const box = scale(zone);
        return (
          <g key={zone.id}>
            <rect
              {...box}
              vectorEffect="non-scaling-stroke"
              strokeDasharray={tone === 'student' ? dash : undefined}
              className={toneClassNames[tone]}
            />
            <circle
              cx={box.x + r}
              cy={box.y + r}
              r={r}
              vectorEffect="non-scaling-stroke"
              className="fill-surface stroke-current"
            />
            <text
              x={box.x + r}
              y={box.y + r}
              textAnchor="middle"
              dominantBaseline="central"
              fontSize={r * 1.2}
              className="fill-text font-semibold"
            >
              {String(index + 1)}
            </text>
          </g>
        );
      })}
      {draft ? (
        <rect
          {...scale(draft)}
          vectorEffect="non-scaling-stroke"
          strokeDasharray={dash}
          className="fill-accent/10 stroke-accent"
        />
      ) : null}
    </svg>
  );
}
