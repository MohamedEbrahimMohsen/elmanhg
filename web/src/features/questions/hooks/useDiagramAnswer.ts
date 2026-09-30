import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { itemsIn, moveItem, placeItem, returnItem, type DiagramPlacements } from '../api/diagramPlacement';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';

export interface DiagramAnswerControls {
  selectedItemId: string | null;
  announcement: string;
  focusItemId: string | null;
  select: (itemId: string) => void;
  clear: () => void;
  place: (itemId: string, zoneId: string) => void;
  placeSelected: (zoneId: string) => void;
  returnToBank: (itemId: string) => void;
  move: (zoneId: string, index: number, delta: -1 | 1) => void;
}

export function useDiagramAnswer(
  diagram: StudentDiagram,
  placements: DiagramPlacements,
  onChange: (next: DiagramPlacements) => void,
): DiagramAnswerControls {
  const { t } = useTranslation('diagramStudent');
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);
  const [announcement, setAnnouncement] = useState('');
  const [focusItemId, setFocusItemId] = useState<string | null>(null);
  const itemText = (itemId: string) => diagram.items.find((item) => item.id === itemId)?.text ?? itemId;
  const zoneNumber = (zoneId: string) => diagram.zones.findIndex((zone) => zone.id === zoneId) + 1;

  const commit = (next: DiagramPlacements, itemId: string, message: string) => {
    onChange(next);
    setSelectedItemId(null);
    setFocusItemId(itemId);
    setAnnouncement(message);
  };

  useEffect(() => {
    if (selectedItemId === null) {
      return undefined;
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setSelectedItemId(null);
        setAnnouncement(t('announce.cleared'));
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
    };
  }, [selectedItemId, t]);

  const place = (itemId: string, zoneId: string) => {
    const next = placeItem(diagram, placements, itemId, zoneId);
    if (next === null) {
      setAnnouncement(t('announce.full', { number: zoneNumber(zoneId) }));
      return;
    }
    const position = itemsIn(next, zoneId).indexOf(itemId) + 1;
    commit(next, itemId, t('announce.placed', { item: itemText(itemId), number: zoneNumber(zoneId), position }));
  };

  return {
    selectedItemId,
    announcement,
    focusItemId,
    select: (itemId) => {
      const picked = selectedItemId !== itemId;
      setSelectedItemId(picked ? itemId : null);
      setAnnouncement(picked ? t('announce.picked', { item: itemText(itemId) }) : t('announce.cleared'));
    },
    clear: () => {
      if (selectedItemId !== null) {
        setSelectedItemId(null);
        setAnnouncement(t('announce.cleared'));
      }
    },
    place,
    placeSelected: (zoneId) => {
      if (selectedItemId === null) {
        setAnnouncement(t('announce.selectFirst'));
        return;
      }
      place(selectedItemId, zoneId);
    },
    returnToBank: (itemId) => {
      commit(returnItem(placements, itemId), itemId, t('announce.returned', { item: itemText(itemId) }));
    },
    move: (zoneId, index, delta) => {
      const itemId = itemsIn(placements, zoneId)[index];
      if (itemId === undefined) {
        return;
      }
      const position = index + delta + 1;
      commit(
        moveItem(placements, zoneId, index, delta),
        itemId,
        t('announce.moved', { item: itemText(itemId), number: zoneNumber(zoneId), position }),
      );
    },
  };
}
