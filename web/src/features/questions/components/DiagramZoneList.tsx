import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { isZoneFull, itemsIn, type DiagramPlacements } from '../api/diagramPlacement';
import type { DiagramItemMark } from '../api/diagramReview';
import type { StudentDiagram } from '../schemas/studentDiagramSchema';
import { DiagramZoneRow, type DiagramZoneRowProps } from './DiagramZoneRow';

export interface DiagramZoneListProps extends Pick<
  DiagramZoneRowProps,
  'chipProps' | 'onPlace' | 'onReturn' | 'onMove'
> {
  diagram: StudentDiagram;
  placements: DiagramPlacements;
  selectedItemId: string | null;
  overZoneId: string | null;
  interactive: boolean;
  marks?: Record<string, DiagramItemMark> | undefined;
}

export function DiagramZoneList({
  diagram,
  placements,
  selectedItemId,
  overZoneId,
  ...rowProps
}: DiagramZoneListProps) {
  const { t } = useTranslation('diagramStudent');
  const headingId = useId();
  const itemOf = (itemId: string) => diagram.items.find((item) => item.id === itemId);
  const selectedItem = selectedItemId === null ? null : (itemOf(selectedItemId) ?? null);

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-2">
      <h3 id={headingId} className="text-h3 font-bold">
        {t('zonesTitle')}
      </h3>
      <ol className="flex flex-col gap-2">
        {diagram.zones.map((zone, index) => (
          <DiagramZoneRow
            key={zone.id}
            zone={zone}
            number={index + 1}
            items={itemsIn(placements, zone.id).flatMap((itemId) => itemOf(itemId) ?? [])}
            selectedItem={selectedItem}
            full={isZoneFull(diagram, placements, zone.id)}
            over={overZoneId === zone.id}
            {...rowProps}
          />
        ))}
      </ol>
    </section>
  );
}
