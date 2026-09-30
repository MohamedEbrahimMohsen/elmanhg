import { useState, type PointerEvent, type PointerEventHandler } from 'react';
import { rectFromPoints, toPercentPoint } from '../api/diagramGeometry';
import type { DiagramRect } from '../api/dragDropValues';
import { diagramZoneMinSize } from '../api/questionOptions';

type Handler = PointerEventHandler<SVGSVGElement>;

export interface ZoneDrawing {
  draft: DiagramRect | null;
  handlers: { onPointerDown: Handler; onPointerMove: Handler; onPointerUp: Handler; onPointerLeave: Handler };
}

const pointOf = (event: PointerEvent<SVGSVGElement>) =>
  toPercentPoint(event.clientX, event.clientY, event.currentTarget.getBoundingClientRect());

export function useZoneDrawing(onDraw: (rect: DiagramRect) => void, enabled: boolean): ZoneDrawing {
  const [start, setStart] = useState<{ x: number; y: number } | null>(null);
  const [draft, setDraft] = useState<DiagramRect | null>(null);
  const reset = () => {
    setStart(null);
    setDraft(null);
  };

  return {
    draft,
    handlers: {
      onPointerDown: (event) => {
        if (!enabled || event.button !== 0) {
          return;
        }
        event.preventDefault();
        const point = pointOf(event);
        if (!point) {
          return;
        }
        setStart(point);
        setDraft({ ...point, width: 0, height: 0 });
      },
      onPointerMove: (event) => {
        const point = pointOf(event);
        if (start && point) {
          setDraft(rectFromPoints(start, point));
        }
      },
      onPointerUp: () => {
        if (draft && draft.width >= diagramZoneMinSize && draft.height >= diagramZoneMinSize) {
          onDraw(draft);
        }
        reset();
      },
      onPointerLeave: reset,
    },
  };
}
