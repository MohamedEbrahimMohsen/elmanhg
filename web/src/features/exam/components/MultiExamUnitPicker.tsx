import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { MultiUnitExamUnitOptionResult } from '@/shared/api/generated/model';

export interface MultiExamUnitPickerProps {
  units: MultiUnitExamUnitOptionResult[];
  selected: string[];
  onToggle: (id: string, checked: boolean) => void;
}

export function MultiExamUnitPicker({ units, selected, onToggle }: MultiExamUnitPickerProps) {
  const { t } = useTranslation('exam');
  const baseId = useId();

  return (
    <fieldset className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <legend className="px-1 text-ui font-semibold text-text">{t('multi.units')}</legend>
      {units.map((unit) => {
        const captionId = `${baseId}-${unit.unitId}`;
        return (
          <div key={unit.unitId} className="flex flex-col">
            <label className="flex min-h-11 items-center gap-3 text-ui text-text">
              <input
                type="checkbox"
                aria-describedby={captionId}
                className="size-5 accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45"
                checked={selected.includes(unit.unitId)}
                disabled={!unit.hasBlueprint}
                onChange={(event) => {
                  onToggle(unit.unitId, event.target.checked);
                }}
              />
              {unit.name}
            </label>
            <p id={captionId} className="ps-8 text-caption text-text-muted">
              {unit.hasBlueprint ? t('multi.available', { count: Number(unit.servableCount) }) : t('multi.noBlueprint')}
            </p>
          </div>
        );
      })}
    </fieldset>
  );
}
