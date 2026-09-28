import { useState } from 'react';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Button } from '@/shared/ui/button';
import type { MoveDirection } from '../api/position';
import type { NameValues } from '../schemas/nameSchema';
import { NameForm } from './NameForm';

export interface ItemActionsProps {
  name: string;
  isFirst: boolean;
  isLast: boolean;
  onMove: (direction: MoveDirection) => void;
  onRename: (name: string) => Promise<void>;
  onDelete: () => void;
  renameLabel: string;
  serverErrorFields: ServerErrorFields<NameValues>;
}

type Mode = 'idle' | 'renaming' | 'confirmingDelete';

export function ItemActions({
  name,
  isFirst,
  isLast,
  onMove,
  onRename,
  onDelete,
  renameLabel,
  serverErrorFields,
}: ItemActionsProps) {
  const { t } = useTranslation('content');
  const [mode, setMode] = useState<Mode>('idle');
  const backToIdle = () => {
    setMode('idle');
  };

  if (mode === 'renaming') {
    return (
      <NameForm
        label={renameLabel}
        submitLabel={t('actions.save')}
        defaultName={name}
        serverErrorFields={serverErrorFields}
        onSubmit={async (newName) => {
          await onRename(newName);
          backToIdle();
        }}
        onCancel={backToIdle}
      />
    );
  }

  if (mode === 'confirmingDelete') {
    return (
      <div className="flex flex-wrap items-center gap-2">
        <p className="text-ui text-text">{t('actions.confirmDelete', { name })}</p>
        <Button
          size="sm"
          variant="danger"
          onClick={() => {
            onDelete();
            backToIdle();
          }}
        >
          {t('actions.confirm')}
        </Button>
        <Button size="sm" variant="secondary" onClick={backToIdle}>
          {t('actions.cancel')}
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button
        size="sm"
        variant="secondary"
        aria-label={t('actions.moveUp', { name })}
        disabled={isFirst}
        onClick={() => {
          onMove('up');
        }}
      >
        <ChevronUp aria-hidden className="size-4" />
      </Button>
      <Button
        size="sm"
        variant="secondary"
        aria-label={t('actions.moveDown', { name })}
        disabled={isLast}
        onClick={() => {
          onMove('down');
        }}
      >
        <ChevronDown aria-hidden className="size-4" />
      </Button>
      <Button
        size="sm"
        variant="secondary"
        onClick={() => {
          setMode('renaming');
        }}
      >
        {t('actions.rename')}
      </Button>
      <Button
        size="sm"
        variant="danger"
        onClick={() => {
          setMode('confirmingDelete');
        }}
      >
        {t('actions.delete')}
      </Button>
    </div>
  );
}
