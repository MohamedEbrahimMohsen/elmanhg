import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { UnitResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { targetPosition } from '../api/position';
import { useUnitMutations } from '../hooks/useUnitMutations';
import { ItemActions } from './ItemActions';
import { UnitLessons } from './UnitLessons';

export interface UnitItemProps {
  subjectId: string;
  unit: UnitResult;
  isFirst: boolean;
  isLast: boolean;
  position: number;
}

const unitNameErrorFields = { UNIT_NAME_REQUIRED: 'name', UNIT_NAME_TOO_LONG: 'name' } as const;

export function UnitItem({ subjectId, unit, isFirst, isLast, position }: UnitItemProps) {
  const { t } = useTranslation('content');
  const { rename, move, remove } = useUnitMutations(subjectId);
  const [isExpanded, setIsExpanded] = useState(false);
  const panelId = useId();

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border bg-bg p-3">
      <span className="text-ui font-semibold text-text">{unit.name}</span>
      <p className="text-caption text-text-muted">{t('units.lessonCount', { count: unit.lessonCount })}</p>
      <ItemActions
        name={unit.name}
        isFirst={isFirst}
        isLast={isLast}
        onMove={(direction) => {
          move(unit.id, targetPosition(position - 1, direction));
        }}
        onRename={(name) => rename(unit.id, name)}
        onDelete={() => {
          remove(unit.id);
        }}
        renameLabel={t('actions.newName')}
        serverErrorFields={unitNameErrorFields}
      />
      <Button
        variant="ghost"
        size="sm"
        className="self-start"
        aria-expanded={isExpanded}
        aria-controls={panelId}
        onClick={() => {
          setIsExpanded((value) => !value);
        }}
      >
        {t(isExpanded ? 'units.hideLessons' : 'units.showLessons')}
      </Button>
      {isExpanded ? <UnitLessons subjectId={subjectId} unitId={unit.id} unitName={unit.name} id={panelId} /> : null}
    </li>
  );
}
