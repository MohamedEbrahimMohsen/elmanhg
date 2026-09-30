import { Plus } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { assignItem, nextItemId, removeItemEverywhere, type DiagramZoneValues } from '../api/dragDropValues';
import { diagramItemsMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { DiagramItemRow } from './DiagramItemRow';

export interface DiagramItemsFieldProps {
  onReplaceZones: (zones: DiagramZoneValues[]) => void;
}

export function DiagramItemsField({ onReplaceZones }: DiagramItemsFieldProps) {
  const { t } = useTranslation('questionsDiagram');
  const items = useFieldArray<QuestionValues, 'diagramItems'>({ name: 'diagramItems' });
  const zones = useWatch<QuestionValues, 'diagramZones'>({ name: 'diagramZones' });
  const values = useWatch<QuestionValues, 'diagramItems'>({ name: 'diagramItems' });
  const { errors } = useFormState<QuestionValues>({ name: 'diagramItems' });
  const message = errors.diagramItems?.message ?? errors.diagramItems?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.dragDrop.itemsLegend')}</legend>
      <p className="text-caption text-text-muted">{t('editor.dragDrop.itemsHint')}</p>
      {items.fields.map((field, index) => {
        const itemId = values[index]?.id ?? '';
        return (
          <DiagramItemRow
            key={field.id}
            index={index}
            itemId={itemId}
            zones={zones}
            onAssign={(zoneId) => {
              onReplaceZones(assignItem(zones, itemId, zoneId));
            }}
            onRemove={() => {
              onReplaceZones(removeItemEverywhere(zones, itemId));
              items.remove(index);
            }}
            canRemove={items.fields.length > 1}
          />
        );
      })}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <div>
        <Button
          variant="secondary"
          size="sm"
          disabled={items.fields.length >= diagramItemsMax}
          onClick={() => {
            items.append({ id: nextItemId(values.map((item) => item.id)), text: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.dragDrop.addItem')}
        </Button>
      </div>
    </fieldset>
  );
}
