import type { DeepPartialSkipArrayKey } from 'react-hook-form';
import type { JsonElement, UpdateQuestionRequest } from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { dragDropBodySchema, dragDropSpecSchema } from '../schemas/dragDropContentSchemas';

export type DiagramZoneValues = QuestionValues['diagramZones'][number];

export interface DiagramRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface DiagramModel {
  image: { url: string; width: number; height: number; alt: string };
  zones: (DiagramRect & { id: string; capacity: number; ordered: boolean; itemIds: string[] })[];
  items: { id: string; text: string }[];
}

export const defaultZoneRect: DiagramRect = { x: 40, y: 40, width: 20, height: 20 };

export function emptyDiagramImage(): QuestionValues['diagramImage'] {
  return { key: '', url: '', width: 0, height: 0, alt: '' };
}

function nextId(prefix: string, ids: readonly string[]): string {
  let candidate = 1;
  while (ids.includes(`${prefix}${String(candidate)}`)) {
    candidate += 1;
  }
  return `${prefix}${String(candidate)}`;
}

export const nextZoneId = (ids: readonly string[]) => nextId('z', ids);

export const nextItemId = (ids: readonly string[]) => nextId('i', ids);

export function formatPercent(value: number): string {
  return String(Math.round(value * 100) / 100);
}

export function newZone(id: string, rect: DiagramRect): DiagramZoneValues {
  return {
    id,
    x: formatPercent(rect.x),
    y: formatPercent(rect.y),
    width: formatPercent(rect.width),
    height: formatPercent(rect.height),
    capacity: '1',
    ordered: false,
    itemIds: [],
  };
}

export function zoneOfItem(zones: readonly DiagramZoneValues[], itemId: string): string {
  return zones.find((zone) => zone.itemIds.includes(itemId))?.id ?? '';
}

export function removeItemEverywhere(zones: readonly DiagramZoneValues[], itemId: string): DiagramZoneValues[] {
  return zones.map((zone) => ({ ...zone, itemIds: zone.itemIds.filter((id) => id !== itemId) }));
}

export function assignItem(zones: readonly DiagramZoneValues[], itemId: string, zoneId: string): DiagramZoneValues[] {
  return removeItemEverywhere(zones, itemId).map((zone) =>
    zone.id === zoneId ? { ...zone, itemIds: [...zone.itemIds, itemId] } : zone,
  );
}

export function moveItemInZone(
  zones: readonly DiagramZoneValues[],
  zoneId: string,
  index: number,
  delta: -1 | 1,
): DiagramZoneValues[] {
  return zones.map((zone) => {
    const target = index + delta;
    const moved = zone.itemIds[index];
    const neighbour = zone.itemIds[target];
    if (zone.id !== zoneId || moved === undefined || neighbour === undefined) {
      return zone;
    }
    const itemIds = [...zone.itemIds];
    itemIds[index] = neighbour;
    itemIds[target] = moved;
    return { ...zone, itemIds };
  });
}

export function readDragDrop(body: JsonElement, spec: JsonElement): Partial<QuestionValues> {
  const parsedBody = dragDropBodySchema.safeParse(body);
  if (!parsedBody.success) {
    return {};
  }
  const keys = dragDropSpecSchema.safeParse(spec).data?.zones ?? [];
  const { image, zones, items } = parsedBody.data;
  return {
    diagramImage: { key: image.key, url: image.url ?? '', width: image.width, height: image.height, alt: image.alt },
    diagramZones: zones.map((zone) => {
      const key = keys.find((entry) => entry.zoneId === zone.id);
      return {
        ...newZone(zone.id, zone),
        capacity: String(zone.capacity),
        ordered: key?.ordered ?? false,
        itemIds: key?.itemIds ?? [],
      };
    }),
    diagramItems: items,
  };
}

export function toDragDropContent(values: QuestionValues): Pick<UpdateQuestionRequest, 'body' | 'gradingSpec'> {
  const { key, width, height, alt } = values.diagramImage;
  return {
    body: {
      image: { key, width, height, alt },
      zones: values.diagramZones.map((zone) => ({
        id: zone.id,
        x: Number(zone.x),
        y: Number(zone.y),
        width: Number(zone.width),
        height: Number(zone.height),
        capacity: Number(zone.capacity),
      })),
      items: values.diagramItems,
    },
    gradingSpec: {
      zones: values.diagramZones.map((zone) => ({ zoneId: zone.id, itemIds: zone.itemIds, ordered: zone.ordered })),
    },
  };
}

const toNumber = (value: string | number | undefined) => (Number.isFinite(Number(value)) ? Number(value) : 0);

export function toDiagramModel(values: DeepPartialSkipArrayKey<QuestionValues>): DiagramModel {
  const image = values.diagramImage;
  return {
    image: {
      url: image?.url ?? '',
      width: toNumber(image?.width),
      height: toNumber(image?.height),
      alt: image?.alt ?? '',
    },
    zones: (values.diagramZones ?? []).map((zone) => ({
      id: zone.id ?? '',
      x: toNumber(zone.x),
      y: toNumber(zone.y),
      width: toNumber(zone.width),
      height: toNumber(zone.height),
      capacity: zone.capacity === undefined ? 1 : toNumber(zone.capacity),
      ordered: zone.ordered ?? false,
      itemIds: (zone.itemIds ?? []).filter((id): id is string => typeof id === 'string'),
    })),
    items: (values.diagramItems ?? []).map((item) => ({ id: item.id ?? '', text: item.text ?? '' })),
  };
}
