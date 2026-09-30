import { Plus } from 'lucide-react';
import { useFormState, type FieldArrayWithId } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import type { DiagramZoneValues } from '../api/dragDropValues';
import { diagramZonesMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DiagramZoneCard } from './DiagramZoneCard';

export interface DiagramZonesFieldProps {
  fields: FieldArrayWithId<QuestionValues, 'diagramZones'>[];
  onAdd: () => void;
  onRemove: (index: number) => void;
  onReplace: (zones: DiagramZoneValues[]) => void;
}

export function DiagramZonesField({ fields, onAdd, onRemove, onReplace }: DiagramZonesFieldProps) {
  const { t } = useTranslation('questionsDiagram');
  const { errors } = useFormState<QuestionValues>({ name: 'diagramZones' });
  const message = errors.diagramZones?.message ?? errors.diagramZones?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.dragDrop.zonesLegend')}</legend>
      {fields.map((field, index) => (
        <DiagramZoneCard
          key={field.id}
          index={index}
          onRemove={() => {
            onRemove(index);
          }}
          onReplace={onReplace}
        />
      ))}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <div>
        <Button variant="secondary" size="sm" disabled={fields.length >= diagramZonesMax} onClick={onAdd}>
          <Plus aria-hidden className="size-4" />
          {t('editor.dragDrop.addZone')}
        </Button>
      </div>
    </fieldset>
  );
}
