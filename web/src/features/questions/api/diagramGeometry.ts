import type { DiagramRect } from './dragDropValues';

export interface CanvasBounds {
  left: number;
  top: number;
  width: number;
  height: number;
}

const hundredths = (value: number) => Math.round(value * 100);

export function round2(value: number): number {
  return hundredths(value) / 100;
}

const clamp = (value: number) => Math.min(100, Math.max(0, value));

export function toPercentPoint(
  clientX: number,
  clientY: number,
  bounds: CanvasBounds,
): { x: number; y: number } | null {
  if (bounds.width <= 0 || bounds.height <= 0) {
    return null;
  }
  return {
    x: round2(clamp(((clientX - bounds.left) / bounds.width) * 100)),
    y: round2(clamp(((clientY - bounds.top) / bounds.height) * 100)),
  };
}

export function rectFromPoints(a: { x: number; y: number }, b: { x: number; y: number }): DiagramRect {
  return {
    x: round2(Math.min(a.x, b.x)),
    y: round2(Math.min(a.y, b.y)),
    width: round2(Math.abs(b.x - a.x)),
    height: round2(Math.abs(b.y - a.y)),
  };
}

function inHundredths(rect: DiagramRect): DiagramRect {
  return {
    x: hundredths(rect.x),
    y: hundredths(rect.y),
    width: hundredths(rect.width),
    height: hundredths(rect.height),
  };
}

export function zonesOverlap(first: DiagramRect, second: DiagramRect): boolean {
  const a = inHundredths(first);
  const b = inHundredths(second);
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

export function isInsideImage(rect: DiagramRect): boolean {
  return hundredths(rect.x) + hundredths(rect.width) <= 10000 && hundredths(rect.y) + hundredths(rect.height) <= 10000;
}
