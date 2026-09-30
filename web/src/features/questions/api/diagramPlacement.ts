import type { StudentDiagram } from '../schemas/studentDiagramSchema';
import type { CanvasBounds } from './diagramGeometry';

export type DiagramPlacements = Record<string, readonly string[]>;

export interface PlacementPayload {
  zoneId: string;
  itemIds: string[];
}

export function itemsIn(placements: DiagramPlacements, zoneId: string): readonly string[] {
  return Object.hasOwn(placements, zoneId) ? (placements[zoneId] ?? []) : [];
}

export function placedZoneOf(placements: DiagramPlacements, itemId: string): string | null {
  return Object.entries(placements).find(([, ids]) => ids.includes(itemId))?.[0] ?? null;
}

export function unplacedItems(diagram: StudentDiagram, placements: DiagramPlacements): StudentDiagram['items'] {
  return diagram.items.filter((item) => placedZoneOf(placements, item.id) === null);
}

export function isZoneFull(diagram: StudentDiagram, placements: DiagramPlacements, zoneId: string): boolean {
  const zone = diagram.zones.find((entry) => entry.id === zoneId);
  return zone !== undefined && itemsIn(placements, zoneId).length >= zone.capacity;
}

export function returnItem(placements: DiagramPlacements, itemId: string): DiagramPlacements {
  return Object.fromEntries(
    Object.entries(placements).map(([zoneId, ids]) => [zoneId, ids.filter((id) => id !== itemId)]),
  );
}

export function placeItem(
  diagram: StudentDiagram,
  placements: DiagramPlacements,
  itemId: string,
  zoneId: string,
): DiagramPlacements | null {
  const zone = diagram.zones.find((entry) => entry.id === zoneId);
  if (!zone) {
    return null;
  }
  const current = itemsIn(placements, zoneId);
  if (current.includes(itemId)) {
    return placements;
  }
  if (current.length >= zone.capacity) {
    return null;
  }
  const rest = returnItem(placements, itemId);
  return { ...rest, [zoneId]: [...itemsIn(rest, zoneId), itemId] };
}

export function moveItem(
  placements: DiagramPlacements,
  zoneId: string,
  index: number,
  delta: -1 | 1,
): DiagramPlacements {
  const ids = itemsIn(placements, zoneId);
  const moved = ids[index];
  const neighbour = ids[index + delta];
  if (moved === undefined || neighbour === undefined) {
    return placements;
  }
  const next = [...ids];
  next[index] = neighbour;
  next[index + delta] = moved;
  return { ...placements, [zoneId]: next };
}

export function toCanvasPercent(
  clientX: number,
  clientY: number,
  bounds: CanvasBounds,
): { x: number; y: number } | null {
  if (bounds.width <= 0 || bounds.height <= 0) {
    return null;
  }
  const x = ((clientX - bounds.left) / bounds.width) * 100;
  const y = ((clientY - bounds.top) / bounds.height) * 100;
  return x < 0 || x > 100 || y < 0 || y > 100 ? null : { x, y };
}

const hundredths = (value: number) => Math.round(value * 100);

// Half-open [start, end) so touching zones never share a point; the image border itself is closed.
const within = (point: number, start: number, size: number) => {
  const from = hundredths(start);
  const to = from + hundredths(size);
  return point >= from && (point < to || (to === 10000 && point <= 10000));
};

export function zoneAtPoint(zones: StudentDiagram['zones'], point: { x: number; y: number }): string | null {
  const px = point.x * 100;
  const py = point.y * 100;
  return zones.find((zone) => within(px, zone.x, zone.width) && within(py, zone.y, zone.height))?.id ?? null;
}

export function toPlacementsPayload(diagram: StudentDiagram, placements: DiagramPlacements): PlacementPayload[] {
  const itemIds = new Set(diagram.items.map((item) => item.id));
  return diagram.zones
    .map((zone) => ({ zoneId: zone.id, itemIds: itemsIn(placements, zone.id).filter((id) => itemIds.has(id)) }))
    .filter((placement) => placement.itemIds.length > 0);
}

export function fromPlacementsPayload(
  payload: readonly { zoneId: string; itemIds: readonly string[] }[],
): DiagramPlacements {
  const placements: Record<string, readonly string[]> = {};
  for (const { zoneId, itemIds } of payload) {
    if (!Object.hasOwn(placements, zoneId)) {
      placements[zoneId] = [...itemIds];
    }
  }
  return placements;
}
