import { useEffect, useRef, useState, type MouseEvent, type PointerEvent, type RefObject } from 'react';
import { toCanvasPercent, zoneAtPoint } from '../api/diagramPlacement';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';

// CSS px a pointer travels before a press becomes a drag, so taps still select.
const dragThreshold = 6;

export interface DiagramDrag {
  itemId: string;
  x: number;
  y: number;
  overZoneId: string | null;
}

export interface UseDiagramDragOptions {
  canvasRef: RefObject<HTMLDivElement | null>;
  zones: StudentDiagram['zones'];
  onDropZone: (itemId: string, zoneId: string) => void;
  onDropBank: (itemId: string) => void;
}

interface Press {
  itemId: string;
  pointerId: number;
  startX: number;
  startY: number;
  dragging: boolean;
}

type DropTarget = { kind: 'zone'; zoneId: string } | { kind: 'bank' } | null;

export function useDiagramDrag({ canvasRef, zones, onDropZone, onDropBank }: UseDiagramDragOptions) {
  const [drag, setDrag] = useState<DiagramDrag | null>(null);
  const press = useRef<Press | null>(null);
  const swallowClickOn = useRef<HTMLElement | null>(null);

  const canvasZoneAt = (x: number, y: number): { inside: boolean; zoneId: string | null } => {
    const bounds = canvasRef.current?.getBoundingClientRect();
    const point = bounds ? toCanvasPercent(x, y, bounds) : null;
    return point ? { inside: true, zoneId: zoneAtPoint(zones, point) } : { inside: false, zoneId: null };
  };

  const targetAt = (x: number, y: number): DropTarget => {
    const canvas = canvasZoneAt(x, y);
    if (canvas.inside) {
      return canvas.zoneId ? { kind: 'zone', zoneId: canvas.zoneId } : null;
    }
    const element = 'elementFromPoint' in document ? document.elementFromPoint(x, y) : null;
    const zoneId = element?.closest('[data-zone-id]')?.getAttribute('data-zone-id');
    if (zoneId) {
      return { kind: 'zone', zoneId };
    }
    return element?.closest('[data-diagram-bank]') ? { kind: 'bank' } : null;
  };

  const cancel = () => {
    press.current = null;
    setDrag(null);
  };

  useEffect(() => {
    const clearSwallow = () => {
      swallowClickOn.current = null;
    };
    window.addEventListener('pointerdown', clearSwallow, true);
    window.addEventListener('keydown', clearSwallow, true);
    return () => {
      window.removeEventListener('pointerdown', clearSwallow, true);
      window.removeEventListener('keydown', clearSwallow, true);
    };
  }, []);

  useEffect(() => {
    if (drag === null) {
      return undefined;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        press.current = null;
        setDrag(null);
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
    };
  }, [drag]);

  const chipHandlers = (itemId: string) => ({
    onPointerDown: (event: PointerEvent<HTMLElement>) => {
      if (event.pointerType === 'mouse' && event.button !== 0) {
        return;
      }
      press.current = {
        itemId,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        dragging: false,
      };
      const element = event.currentTarget;
      if ('setPointerCapture' in element) {
        element.setPointerCapture(event.pointerId);
      }
    },
    onPointerMove: (event: PointerEvent<HTMLElement>) => {
      const current = press.current;
      if (current?.pointerId !== event.pointerId) {
        return;
      }
      if (
        !current.dragging &&
        Math.hypot(event.clientX - current.startX, event.clientY - current.startY) < dragThreshold
      ) {
        return;
      }
      current.dragging = true;
      setDrag({
        itemId,
        x: event.clientX,
        y: event.clientY,
        overZoneId: canvasZoneAt(event.clientX, event.clientY).zoneId,
      });
    },
    onPointerUp: (event: PointerEvent<HTMLElement>) => {
      const current = press.current;
      if (current?.pointerId !== event.pointerId) {
        return;
      }
      cancel();
      if (!current.dragging) {
        return;
      }
      swallowClickOn.current = event.currentTarget;
      const target = targetAt(event.clientX, event.clientY);
      if (target?.kind === 'zone') {
        onDropZone(itemId, target.zoneId);
      } else if (target?.kind === 'bank') {
        onDropBank(itemId);
      }
    },
    onPointerCancel: cancel,
    onClickCapture: (event: MouseEvent<HTMLElement>) => {
      if (swallowClickOn.current === event.currentTarget) {
        swallowClickOn.current = null;
        event.preventDefault();
        event.stopPropagation();
      }
    },
  });

  return { drag, chipHandlers };
}

export type DiagramChipHandlers = ReturnType<ReturnType<typeof useDiagramDrag>['chipHandlers']>;
