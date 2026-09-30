import { describe, expect, it } from 'vitest';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';
import {
  fromPlacementsPayload,
  moveItem,
  placeItem,
  returnItem,
  toCanvasPercent,
  toPlacementsPayload,
  unplacedItems,
  zoneAtPoint,
} from './diagramPlacement';

const diagram: StudentDiagram = {
  image: { url: '/api/media/question-diagrams/l/abc.png', width: 800, height: 600, alt: 'Plant cell' },
  zones: [
    { id: 'z1', x: 10, y: 10, width: 20, height: 15, capacity: 2 },
    { id: 'z2', x: 50, y: 40, width: 30, height: 20.5, capacity: 2 },
  ],
  items: [
    { id: 'i1', text: 'Nucleus' },
    { id: 'i2', text: 'Vacuole' },
    { id: 'i3', text: 'Wall' },
    { id: 'i4', text: 'Membrane' },
    { id: 'i5', text: 'Engine' },
  ],
};

const zone = (id: string, x: number, width: number) => ({ id, x, y: 0, width, height: 100, capacity: 1 });

describe('diagramPlacement', () => {
  it('maps a point to the zone that contains it', () => {
    expect(zoneAtPoint(diagram.zones, { x: 20, y: 15 })).toBe('z1');
  });

  it('gives a point on a shared edge to the zone that starts there', () => {
    expect(zoneAtPoint([zone('left', 0, 50), zone('right', 50, 50)], { x: 50, y: 10 })).toBe('right');
  });

  it('closes a zone on the right and bottom border of the image', () => {
    expect(zoneAtPoint([zone('edge', 80, 20)], { x: 100, y: 100 })).toBe('edge');
  });

  it('compares in hundredths so float sums never cross a bound', () => {
    const zones = [zone('thin', 10, 20.1)];

    expect(zoneAtPoint(zones, { x: 30.099, y: 50 })).toBe('thin');
    expect(zoneAtPoint(zones, { x: 30.1, y: 50 })).toBeNull();
    expect(zoneAtPoint([zone('shifted', 0.01, 20.1)], { x: 20.11, y: 50 })).toBeNull();
  });

  it('returns null outside every zone', () => {
    expect(zoneAtPoint(diagram.zones, { x: 90, y: 90 })).toBeNull();
  });

  it('converts a client point to unrounded percent and null outside the canvas', () => {
    const bounds = { left: 0, top: 0, width: 800, height: 600 };

    const point = toCanvasPercent(200, 100, bounds);

    expect(point?.x).toBe(25);
    expect(point?.y).toBeCloseTo(16.6667, 4);
    expect(point?.y).not.toBe(16.67);
    expect(toCanvasPercent(801, 10, bounds)).toBeNull();
  });

  it('places an item at the end of a zone and removes it from its previous zone', () => {
    const placements = { z1: ['i1', 'i2'], z2: ['i3'] };

    expect(placeItem(diagram, placements, 'i1', 'z2')).toEqual({ z1: ['i2'], z2: ['i3', 'i1'] });
  });

  it('refuses to place an item in a full zone', () => {
    expect(placeItem(diagram, { z1: ['i1', 'i2'] }, 'i3', 'z1')).toBeNull();
  });

  it('returns an item to the bank and lists unplaced items in bank order', () => {
    const placements = returnItem({ z1: ['i4', 'i1'] }, 'i4');

    expect(placements).toEqual({ z1: ['i1'] });
    expect(unplacedItems(diagram, placements).map((item) => item.id)).toEqual(['i2', 'i3', 'i4', 'i5']);
  });

  it('moves an item within a zone', () => {
    expect(moveItem({ z2: ['i4', 'i3'] }, 'z2', 1, -1)).toEqual({ z2: ['i3', 'i4'] });
    expect(moveItem({ z2: ['i4', 'i3'] }, 'z2', 1, 1)).toEqual({ z2: ['i4', 'i3'] });
  });

  it('builds the payload in zone order without empty zones or unknown items', () => {
    expect(toPlacementsPayload(diagram, { z2: ['i4', 'x9'], z1: [] })).toEqual([{ zoneId: 'z2', itemIds: ['i4'] }]);
  });

  it('reads placements from a payload keeping the first entry of a zone', () => {
    expect(
      fromPlacementsPayload([
        { zoneId: 'z1', itemIds: ['i1'] },
        { zoneId: 'z1', itemIds: ['i2'] },
        { zoneId: 'z2', itemIds: ['i4', 'i3'] },
      ]),
    ).toEqual({ z1: ['i1'], z2: ['i4', 'i3'] });
  });
});
