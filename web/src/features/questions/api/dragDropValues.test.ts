import { describe, expect, it } from 'vitest';
import { emptyQuestionValues } from './questionValues';
import {
  assignItem,
  moveItemInZone,
  newZone,
  nextItemId,
  nextZoneId,
  readDragDrop,
  removeItemEverywhere,
  toDiagramModel,
  toDragDropContent,
  type DiagramZoneValues,
} from './dragDropValues';

const key = 'question-diagrams/0b5c1f4e-3d2a-4c8e-9f1a-2b3c4d5e6f70/0123456789abcdef0123456789abcdef.png';

const body = {
  image: { key, url: `/api/media/${key}`, width: 800, height: 600, alt: 'Plant cell' },
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

const spec = {
  zones: [
    { zoneId: 'z1', itemIds: ['i1', 'i2'], ordered: false },
    { zoneId: 'z2', itemIds: ['i4', 'i3'], ordered: true },
  ],
};

const zone = (id: string, itemIds: string[]): DiagramZoneValues => ({
  ...newZone(id, { x: 0, y: 0, width: 10, height: 10 }),
  itemIds,
});

describe('dragDropValues', () => {
  it('reads a stored diagram and key into editor values', () => {
    const values = readDragDrop(body, spec);

    expect(values.diagramImage).toEqual({ key, url: `/api/media/${key}`, width: 800, height: 600, alt: 'Plant cell' });
    expect(values.diagramZones?.[0]).toEqual({
      id: 'z1',
      x: '10',
      y: '10',
      width: '20',
      height: '15',
      capacity: '2',
      ordered: false,
      itemIds: ['i1', 'i2'],
    });
    expect(values.diagramZones?.[1]).toEqual(
      expect.objectContaining({ height: '20.5', ordered: true, itemIds: ['i4', 'i3'] }),
    );
    expect(values.diagramItems).toEqual(body.items);
  });

  it('builds the diagram body and answer key from values', () => {
    const values = { ...emptyQuestionValues('DragDrop'), ...readDragDrop(body, spec) };

    const content = toDragDropContent(values);

    expect(content.body).toEqual({
      image: { key, width: 800, height: 600, alt: 'Plant cell' },
      zones: body.zones,
      items: body.items,
    });
    expect(content.gradingSpec).toEqual(spec);
  });

  it('moves an item into exactly one zone or back to the bank', () => {
    const zones = [zone('z1', ['i1']), zone('z2', [])];

    const moved = assignItem(zones, 'i1', 'z2');

    expect(moved.map((entry) => entry.itemIds)).toEqual([[], ['i1']]);
    expect(assignItem(moved, 'i1', '').map((entry) => entry.itemIds)).toEqual([[], []]);
  });

  it('removes a deleted item from every zone', () => {
    const zones = [zone('z1', ['i1', 'i2']), zone('z2', ['i3'])];

    expect(removeItemEverywhere(zones, 'i2').map((entry) => entry.itemIds)).toEqual([['i1'], ['i3']]);
  });

  it('reorders an item inside a zone and ignores moves past the ends', () => {
    const zones = [zone('z1', ['a', 'b', 'c'])];

    expect(moveItemInZone(zones, 'z1', 1, -1)[0]?.itemIds).toEqual(['b', 'a', 'c']);
    expect(moveItemInZone(zones, 'z1', 1, 1)[0]?.itemIds).toEqual(['a', 'c', 'b']);
    expect(moveItemInZone(zones, 'z1', 0, -1)[0]?.itemIds).toEqual(['a', 'b', 'c']);
    expect(moveItemInZone(zones, 'z1', 2, 1)[0]?.itemIds).toEqual(['a', 'b', 'c']);
  });

  it('picks the first free zone and item ids', () => {
    expect(nextZoneId(['z1', 'z3'])).toBe('z2');
    expect(nextItemId([])).toBe('i1');
  });

  it('builds a numeric model from partial editor values', () => {
    const model = toDiagramModel({ diagramZones: [{ id: 'z1', x: 'abc', y: '5', width: '10', height: '10' }] });

    expect(model.zones[0]).toEqual(
      expect.objectContaining({ id: 'z1', x: 0, y: 5, capacity: 1, ordered: false, itemIds: [] }),
    );
    expect(model.image).toEqual({ url: '', width: 0, height: 0, alt: '' });
  });
});
