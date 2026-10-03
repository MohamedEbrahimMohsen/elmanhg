import type { RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { itemsIn, type DiagramPlacements } from '../api/diagramPlacement';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';

export interface DiagramAnswerCanvasProps {
  diagram: StudentDiagram;
  placements: DiagramPlacements;
  overZoneId: string | null;
  selectedItemId: string | null;
  interactive: boolean;
  onZone: (zoneId: string) => void;
  canvasRef: RefObject<HTMLDivElement | null>;
}

export function DiagramAnswerCanvas({
  diagram,
  placements,
  overZoneId,
  selectedItemId,
  interactive,
  onZone,
  canvasRef,
}: DiagramAnswerCanvasProps) {
  const { t } = useTranslation('diagramStudent');
  const { image } = diagram;

  return (
    <div
      ref={canvasRef}
      role="group"
      aria-label={t('canvasLabel')}
      dir="ltr"
      className="relative w-full overflow-hidden rounded-md border border-border bg-surface"
      style={{ aspectRatio: `${String(image.width)} / ${String(image.height)}` }}
    >
      <img
        src={image.url}
        alt={image.alt}
        width={image.width}
        height={image.height}
        draggable={false}
        decoding="async"
        className="absolute inset-0 size-full"
      />
      {diagram.zones.map((zone, index) => {
        const count = itemsIn(placements, zone.id).length;
        return (
          <button
            key={zone.id}
            type="button"
            data-zone-id={zone.id}
            aria-label={t('zoneButton', { number: index + 1, count, capacity: zone.capacity })}
            disabled={!interactive}
            onClick={() => {
              onZone(zone.id);
            }}
            className={cn(
              'absolute rounded-sm border-2 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden',
              count > 0 ? 'border-solid border-text' : 'border-dashed border-text-muted bg-surface/60',
              selectedItemId !== null && 'focus-visible:border-accent focus-visible:bg-accent/15',
              overZoneId === zone.id && 'border-accent bg-accent/15',
            )}
            style={{
              insetInlineStart: `${String(zone.x)}%`,
              top: `${String(zone.y)}%`,
              width: `${String(zone.width)}%`,
              height: `${String(zone.height)}%`,
            }}
          >
            <span className="absolute start-1 top-1 flex size-6 items-center justify-center rounded-full border border-border-strong bg-surface text-micro font-semibold text-text">
              {index + 1}
            </span>
            {count > 0 ? (
              <span className="absolute end-1 bottom-1 rounded-full bg-accent px-1.5 text-micro font-semibold text-surface">
                {t('zoneFill', { count, capacity: zone.capacity })}
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
