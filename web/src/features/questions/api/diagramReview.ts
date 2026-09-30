import type { DiagramKey } from '../schemas/studentDiagramSchema';
import { itemsIn, type DiagramPlacements } from './diagramPlacement';

export type DiagramItemMark = 'correct' | 'wrong' | 'missed';

interface KeyPlace {
  zoneId: string;
  index: number;
  ordered: boolean;
}

function keyPlaces(diagramKey: DiagramKey): Map<string, KeyPlace> {
  const places = new Map<string, KeyPlace>();
  for (const zone of diagramKey) {
    zone.itemIds.forEach((itemId, index) => {
      if (!places.has(itemId)) {
        places.set(itemId, { zoneId: zone.zoneId, index, ordered: zone.ordered });
      }
    });
  }
  return places;
}

export function markPlacements(diagramKey: DiagramKey, placements: DiagramPlacements): Record<string, DiagramItemMark> {
  const places = keyPlaces(diagramKey);
  const marks: Record<string, DiagramItemMark> = {};
  for (const zone of diagramKey) {
    let index = 0;
    for (const itemId of itemsIn(placements, zone.zoneId)) {
      if (Object.hasOwn(marks, itemId)) {
        continue;
      }
      const place = places.get(itemId);
      const right = place?.zoneId === zone.zoneId && (!place.ordered || place.index === index);
      marks[itemId] = right ? 'correct' : 'wrong';
      index += 1;
    }
  }
  for (const itemId of places.keys()) {
    if (!Object.hasOwn(marks, itemId)) {
      marks[itemId] = 'missed';
    }
  }
  return marks;
}
