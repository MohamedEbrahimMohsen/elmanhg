import { Trash2 } from 'lucide-react';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { zoneOfItem, type DiagramZoneValues } from '../api/dragDropValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface DiagramItemRowProps {
  index: number;
  itemId: string;
  zones: DiagramZoneValues[];
  onAssign: (zoneId: string) => void;
  onRemove: () => void;
  canRemove: boolean;
}

export function DiagramItemRow({ index, itemId, zones, onAssign, onRemove, canRemove }: DiagramItemRowProps) {
  const { t } = useTranslation('questionsDiagram');
  const id = useId();
  const number = index + 1;

  return (
    <div className="flex items-start gap-2">
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <TextField<QuestionValues>
          name={`diagramItems.${index.toString()}.text` as `diagramItems.${number}.text`}
          label={t('editor.dragDrop.item', { number })}
        />
        <div className="flex flex-col gap-1.5">
          <Label htmlFor={id}>{t('editor.dragDrop.itemZone', { number })}</Label>
          <Select
            id={id}
            value={zoneOfItem(zones, itemId)}
            onChange={(event) => {
              onAssign(event.target.value);
            }}
          >
            <option value="">{t('editor.dragDrop.noZone')}</option>
            {zones.map((zone, position) => (
              <option key={zone.id} value={zone.id}>
                {t('editor.dragDrop.zone', { number: position + 1 })}
              </option>
            ))}
          </Select>
        </div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="mt-6.5"
        aria-label={t('editor.dragDrop.removeItem', { number })}
        disabled={!canRemove}
        onClick={onRemove}
      >
        <Trash2 aria-hidden className="size-4" />
      </Button>
    </div>
  );
}
