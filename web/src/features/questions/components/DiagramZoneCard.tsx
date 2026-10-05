import { ChevronDown, ChevronUp, Trash2 } from 'lucide-react';
import { useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { moveItemInZone, type DiagramZoneValues } from '../api/dragDropValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { CheckboxField } from './CheckboxField';

export interface DiagramZoneCardProps {
  index: number;
  onRemove: () => void;
  onReplace: (zones: DiagramZoneValues[]) => void;
}

type ZoneField = 'x' | 'y' | 'width' | 'height' | 'capacity' | 'ordered';

export function DiagramZoneCard({ index, onRemove, onReplace }: DiagramZoneCardProps) {
  const { t } = useTranslation('questionsDiagram');
  const number = index + 1;
  const zones = useWatch<QuestionValues, 'diagramZones'>({ name: 'diagramZones' });
  const items = useWatch<QuestionValues, 'diagramItems'>({ name: 'diagramItems' });
  const zone = zones[index];
  const name = (field: ZoneField) =>
    `diagramZones.${index.toString()}.${field}` as `diagramZones.${number}.${ZoneField}`;
  const assigned = (zone?.itemIds ?? []).map((id) => ({ id, text: items.find((item) => item.id === id)?.text ?? id }));
  const move = (position: number, delta: -1 | 1) => {
    onReplace(moveItemInZone(zones, zone?.id ?? '', position, delta));
  };

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border p-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-ui font-bold">{t('editor.dragDrop.zone', { number })}</p>
        <Button variant="ghost" size="sm" aria-label={t('editor.dragDrop.removeZone', { number })} onClick={onRemove}>
          <Trash2 aria-hidden className="size-4" />
        </Button>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <TextField<QuestionValues> name={name('x')} label={t('editor.dragDrop.zoneX', { number })} dir="ltr" />
        <TextField<QuestionValues> name={name('y')} label={t('editor.dragDrop.zoneY', { number })} dir="ltr" />
        <TextField<QuestionValues> name={name('width')} label={t('editor.dragDrop.zoneWidth', { number })} dir="ltr" />
        <TextField<QuestionValues>
          name={name('height')}
          label={t('editor.dragDrop.zoneHeight', { number })}
          dir="ltr"
        />
      </div>
      <TextField<QuestionValues>
        name={name('capacity')}
        label={t('editor.dragDrop.zoneCapacity', { number })}
        dir="ltr"
      />
      <CheckboxField<QuestionValues> name={name('ordered')} label={t('editor.dragDrop.zoneOrdered', { number })} />
      <p className="text-caption text-text-muted">{t('editor.dragDrop.zoneItems', { number })}</p>
      {assigned.length === 0 ? (
        <p className="text-caption text-text-muted">{t('editor.dragDrop.zoneEmpty')}</p>
      ) : (
        <ol className="flex flex-col gap-1">
          {assigned.map((item, position) => (
            <li key={item.id} className="flex min-h-9 items-center gap-2 text-ui">
              <span className="flex-1">{item.text}</span>
              {zone?.ordered ? (
                <>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={t('editor.dragDrop.moveUp', { item: item.text })}
                    disabled={position === 0}
                    onClick={() => {
                      move(position, -1);
                    }}
                  >
                    <ChevronUp aria-hidden className="size-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    aria-label={t('editor.dragDrop.moveDown', { item: item.text })}
                    disabled={position === assigned.length - 1}
                    onClick={() => {
                      move(position, 1);
                    }}
                  >
                    <ChevronDown aria-hidden className="size-4" />
                  </Button>
                </>
              ) : null}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
