import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { MultiUnitExamUnitOptionResult } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { optionCardClassName, optionCardDisabledClassName, optionCardIdleClassName } from '@/shared/ui/optionCard';

export interface MultiExamUnitPickerProps {
  units: MultiUnitExamUnitOptionResult[];
  selected: string[];
  onToggle: (id: string, checked: boolean) => void;
}

export function MultiExamUnitPicker({ units, selected, onToggle }: MultiExamUnitPickerProps) {
  const { t } = useTranslation('exam');
  const baseId = useId();

  return (
    <fieldset className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <legend className="text-ui font-semibold text-text">{t('multi.units')}</legend>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
        {units.map((unit) => {
          const nameId = `${baseId}-${unit.unitId}-name`;
          const captionId = `${baseId}-${unit.unitId}-caption`;
          return (
            <label
              key={unit.unitId}
              className={cn(
                optionCardClassName,
                unit.hasBlueprint ? optionCardIdleClassName : optionCardDisabledClassName,
              )}
            >
              <input
                type="checkbox"
                aria-labelledby={nameId}
                aria-describedby={captionId}
                className="size-4.5 shrink-0 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
                checked={selected.includes(unit.unitId)}
                disabled={!unit.hasBlueprint}
                onChange={(event) => {
                  onToggle(unit.unitId, event.target.checked);
                }}
              />
              <span className="flex min-w-0 flex-col">
                <span id={nameId} className="font-semibold text-text">
                  {unit.name}
                </span>
                <span id={captionId} className="text-caption text-text-muted">
                  {unit.hasBlueprint
                    ? t('multi.available', { count: Number(unit.servableCount) })
                    : t('multi.noBlueprint')}
                </span>
              </span>
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
