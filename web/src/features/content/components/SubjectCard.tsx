import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { SubjectResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { targetPosition } from '../api/position';
import { useSubjectMutations } from '../hooks/useSubjectMutations';
import { ItemActions } from './ItemActions';
import { UnitPanel } from './UnitPanel';

export interface SubjectCardProps {
  subject: SubjectResult;
  isFirst: boolean;
  isLast: boolean;
  position: number;
}

const subjectNameErrorFields = { SUBJECT_NAME_REQUIRED: 'name', SUBJECT_NAME_TOO_LONG: 'name' } as const;

export function SubjectCard({ subject, isFirst, isLast, position }: SubjectCardProps) {
  const { t } = useTranslation('content');
  const { rename, move, remove } = useSubjectMutations();
  const [isExpanded, setIsExpanded] = useState(false);
  const panelId = useId();

  return (
    <li className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <div className="flex flex-col gap-1">
        <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{subject.name}</h2>
        <p className="text-caption text-text-muted">{t('subjects.unitCount', { count: subject.unitCount })}</p>
      </div>
      <ItemActions
        name={subject.name}
        isFirst={isFirst}
        isLast={isLast}
        onMove={(direction) => {
          move(subject.id, targetPosition(position - 1, direction));
        }}
        onRename={(name) => rename(subject.id, name)}
        onDelete={() => {
          remove(subject.id);
        }}
        renameLabel={t('actions.newName')}
        serverErrorFields={subjectNameErrorFields}
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
        {t(isExpanded ? 'subjects.hideUnits' : 'subjects.showUnits')}
      </Button>
      {isExpanded ? <UnitPanel subjectId={subject.id} id={panelId} /> : null}
    </li>
  );
}
