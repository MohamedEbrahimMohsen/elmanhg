import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { ExamBlueprintInput, ExamBlueprintResult, UnitExamBlueprintResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { findShortfall, toFormValues } from '../api/blueprintValues';
import { BlueprintEditor } from './BlueprintEditor';
import { ShortfallNotice } from './ShortfallNotice';

export interface UnitBlueprintSectionProps {
  unit: UnitExamBlueprintResult;
  defaultBlueprint: ExamBlueprintResult | null | undefined;
  onSave: (unitId: string, input: ExamBlueprintInput) => Promise<void>;
  onDelete: (examBlueprintId: string) => void;
}

type Mode = 'idle' | 'creating' | 'confirmingDelete';

export function UnitBlueprintSection({ unit, defaultBlueprint, onSave, onDelete }: UnitBlueprintSectionProps) {
  const { t } = useTranslation('blueprints');
  const [mode, setMode] = useState<Mode>('idle');
  const title = t('editor.unitTitle', { name: unit.name });
  const backToIdle = () => {
    setMode('idle');
  };
  const save = (input: ExamBlueprintInput) => onSave(unit.unitId, input);

  if (unit.blueprint) {
    const blueprintId = unit.blueprint.id;
    const actions =
      mode === 'confirmingDelete' ? (
        <div className="flex flex-wrap items-center gap-2">
          <p className="text-ui text-text">{t('editor.confirmRevert')}</p>
          <Button
            size="sm"
            variant="danger"
            onClick={() => {
              onDelete(blueprintId);
              backToIdle();
            }}
          >
            {t('editor.confirm')}
          </Button>
          <Button size="sm" variant="secondary" onClick={backToIdle}>
            {t('editor.cancel')}
          </Button>
        </div>
      ) : (
        <Button
          variant="secondary"
          onClick={() => {
            setMode('confirmingDelete');
          }}
        >
          {t('editor.revert')}
        </Button>
      );
    return (
      <BlueprintEditor
        key={blueprintId}
        title={title}
        defaults={toFormValues(unit.blueprint)}
        available={unit.servable}
        onSave={save}
        actions={actions}
      />
    );
  }

  if (mode === 'creating') {
    return (
      <BlueprintEditor
        title={title}
        defaults={toFormValues(defaultBlueprint)}
        available={unit.servable}
        onSave={async (input) => {
          await save(input);
          backToIdle();
        }}
        onCancel={backToIdle}
      />
    );
  }

  return (
    <section aria-label={title} className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h3 font-bold">{title}</h2>
      <p className="text-ui text-text">{t(defaultBlueprint ? 'editor.usesDefault' : 'editor.noDefaultForUnit')}</p>
      {defaultBlueprint ? (
        <ShortfallNotice
          title={t('editor.unitShortfall')}
          shortfalls={findShortfall(defaultBlueprint.typeCounts, unit.servable)}
        />
      ) : null}
      <div>
        <Button
          variant="secondary"
          onClick={() => {
            setMode('creating');
          }}
        >
          {t('editor.createForUnit')}
        </Button>
      </div>
    </section>
  );
}
