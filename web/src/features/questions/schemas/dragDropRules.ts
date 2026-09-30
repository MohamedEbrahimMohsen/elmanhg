import { isInsideImage, zonesOverlap } from '../api/diagramGeometry';
import { diagramItemTextMax, diagramZoneCapacityMax, diagramZoneMinSize } from '../api/questionOptions';
import type { QuestionValues } from './questionEditorSchema';

type DragDropRuleInput = Pick<QuestionValues, 'diagramImage' | 'diagramZones' | 'diagramItems'>;
type IssueSink = (path: (string | number)[], message: string) => void;
type Zone = QuestionValues['diagramZones'][number];

const errorKey = (key: string) => `questionsDiagram:editor.errors.${key}`;
const percentPattern = /^\d{1,3}(\.\d{1,2})?$/;
const minimumOrderedItems = 2;

const isPercent = (value: string) => percentPattern.test(value) && Number(value) <= 100;

function isSize(position: string, size: string): boolean {
  return (
    isPercent(size) &&
    Number(size) >= diagramZoneMinSize &&
    (!isPercent(position) || isInsideImage({ x: Number(position), y: 0, width: Number(size), height: 0 }))
  );
}

function isValidGeometry(zone: Zone): boolean {
  return isPercent(zone.x) && isPercent(zone.y) && isSize(zone.x, zone.width) && isSize(zone.y, zone.height);
}

function addZoneIssues(zone: Zone, index: number, issue: IssueSink): void {
  const path = (field: string) => ['diagramZones', index, field];
  if (!isPercent(zone.x)) {
    issue(path('x'), errorKey('zonePosition'));
  }
  if (!isPercent(zone.y)) {
    issue(path('y'), errorKey('zonePosition'));
  }
  if (!isSize(zone.x, zone.width)) {
    issue(path('width'), errorKey('zoneSize'));
  }
  if (!isSize(zone.y, zone.height)) {
    issue(path('height'), errorKey('zoneSize'));
  }
  const capacity = Number(zone.capacity);
  if (!/^\d+$/.test(zone.capacity) || capacity < 1 || capacity > diagramZoneCapacityMax) {
    issue(path('capacity'), errorKey('zoneCapacity'));
  } else if (zone.itemIds.length > capacity) {
    issue(path('capacity'), errorKey('zoneOverCapacity'));
  }
  if (zone.ordered && zone.itemIds.length < minimumOrderedItems) {
    issue(path('ordered'), errorKey('zoneOrder'));
  }
}

function hasOverlap(zones: readonly Zone[]): boolean {
  const rects = zones.map((zone) => ({
    x: Number(zone.x),
    y: Number(zone.y),
    width: Number(zone.width),
    height: Number(zone.height),
  }));
  return rects.some((rect, index) => rects.slice(index + 1).some((other) => zonesOverlap(rect, other)));
}

export function addDragDropIssues(values: DragDropRuleInput, issue: IssueSink): void {
  if (values.diagramImage.key === '') {
    issue(['diagramImage'], errorKey('diagramImage'));
  }
  if (values.diagramImage.alt.trim() === '') {
    issue(['diagramImage', 'alt'], 'validation.required');
  }
  const zones = values.diagramZones;
  if (zones.length === 0) {
    issue(['diagramZones'], errorKey('zonesCount'));
  }
  zones.forEach((zone, index) => {
    addZoneIssues(zone, index, issue);
  });
  if (zones.every(isValidGeometry) && hasOverlap(zones)) {
    issue(['diagramZones'], errorKey('zonesOverlap'));
  }
  if (zones.length > 0 && zones.every((zone) => zone.itemIds.length === 0)) {
    issue(['diagramZones'], errorKey('keyEmpty'));
  }
  if (values.diagramItems.length === 0) {
    issue(['diagramItems'], errorKey('itemsCount'));
  }
  values.diagramItems.forEach((item, index) => {
    const text = item.text.trim();
    if (text === '') {
      issue(['diagramItems', index, 'text'], 'validation.required');
    } else if (text.length > diagramItemTextMax) {
      issue(['diagramItems', index, 'text'], errorKey('itemText'));
    }
  });
}
