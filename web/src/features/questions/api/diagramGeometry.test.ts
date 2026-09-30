import { describe, expect, it } from 'vitest';
import { rectFromPoints, toPercentPoint, zonesOverlap } from './diagramGeometry';

const bounds = { left: 0, top: 0, width: 400, height: 300 };

describe('diagramGeometry', () => {
  it('converts a pointer position to image percent, clamped and rounded', () => {
    expect(toPercentPoint(100, 75, bounds)).toEqual({ x: 25, y: 25 });
    expect(toPercentPoint(-5, 400, bounds)).toEqual({ x: 0, y: 100 });
    expect(toPercentPoint(1, 1, { left: 0, top: 0, width: 3, height: 3 })).toEqual({ x: 33.33, y: 33.33 });
  });

  it('returns no point for an unmeasured canvas', () => {
    expect(toPercentPoint(10, 10, { ...bounds, width: 0 })).toBeNull();
  });

  it('normalises a drag in any direction into a rectangle', () => {
    const a = { x: 10, y: 20 };
    const b = { x: 40.5, y: 5 };

    expect(rectFromPoints(b, a)).toEqual(rectFromPoints(a, b));
    expect(rectFromPoints(a, b)).toEqual({ x: 10, y: 5, width: 30.5, height: 15 });
  });

  it('treats touching zones as separate and overlapping zones as overlapping', () => {
    const left = { x: 0, y: 10, width: 50, height: 20 };

    expect(zonesOverlap(left, { x: 50, y: 10, width: 50, height: 20 })).toBe(false);
    expect(zonesOverlap(left, { x: 49.99, y: 10, width: 50, height: 20 })).toBe(true);
  });
});
