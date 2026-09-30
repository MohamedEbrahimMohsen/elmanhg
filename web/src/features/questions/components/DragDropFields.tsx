import { useFieldArray, useWatch } from 'react-hook-form';
import { defaultZoneRect, newZone, nextZoneId } from '../api/dragDropValues';
import { diagramZonesMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DiagramCanvasEditor } from './DiagramCanvasEditor';
import { DiagramImageField } from './DiagramImageField';
import { DiagramItemsField } from './DiagramItemsField';
import { DiagramZonesField } from './DiagramZonesField';
import { registerDiagramLocales } from '../diagramLocales';

registerDiagramLocales();

export interface DragDropFieldsProps {
  lessonId: string;
}

export function DragDropFields({ lessonId }: DragDropFieldsProps) {
  const zones = useFieldArray<QuestionValues, 'diagramZones'>({ name: 'diagramZones' });
  const zoneValues = useWatch<QuestionValues, 'diagramZones'>({ name: 'diagramZones' });
  const image = useWatch<QuestionValues, 'diagramImage'>({ name: 'diagramImage' });
  const ids = zoneValues.map((zone) => zone.id);

  return (
    <div className="flex flex-col gap-4">
      <DiagramImageField lessonId={lessonId} />
      <DiagramCanvasEditor
        image={image}
        zones={zoneValues}
        canDraw={zones.fields.length < diagramZonesMax}
        onDraw={(rect) => {
          zones.append(newZone(nextZoneId(ids), rect));
        }}
      />
      <DiagramZonesField
        fields={zones.fields}
        onAdd={() => {
          zones.append(newZone(nextZoneId(ids), defaultZoneRect));
        }}
        onRemove={zones.remove}
        onReplace={zones.replace}
      />
      <DiagramItemsField onReplaceZones={zones.replace} />
    </div>
  );
}
