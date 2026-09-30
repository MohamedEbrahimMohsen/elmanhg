import type { ReactNode } from 'react';
import { ArrowDown, ArrowUp, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { stepFieldId, type MathStep } from '../api/mathStepsValue';
import type { MathStepsEditor } from '../hooks/useMathStepsEditor';
import { MathField } from './MathField';

export interface MathStepRowProps {
  step: MathStep;
  index: number;
  count: number;
  disabled: boolean;
  keypadOpen: boolean;
  editor: MathStepsEditor;
  keypad: ReactNode;
}

export function MathStepRow({ step, index, count, disabled, keypadOpen, editor, keypad }: MathStepRowProps) {
  const { t } = useTranslation('mathSteps');
  const number = index + 1;

  return (
    <li className="flex flex-col gap-3 rounded-md border border-border bg-surface p-3">
      <MathField
        field={stepFieldId(step.id)}
        label={t('steps.label', { number })}
        value={step.latex}
        rows={2}
        disabled={disabled}
        keypadOpen={keypadOpen}
        editor={editor}
      />
      {keypad}
      <div className="flex justify-end gap-2">
        <Button
          variant="ghost"
          className="min-w-11 px-0"
          aria-label={t('steps.moveUp', { number })}
          ref={editor.focus.registerMoveButton(step.id, -1)}
          disabled={disabled || index === 0}
          onClick={() => {
            editor.move(step.id, -1);
          }}
        >
          <ArrowUp aria-hidden className="size-5" />
        </Button>
        <Button
          variant="ghost"
          className="min-w-11 px-0"
          aria-label={t('steps.moveDown', { number })}
          ref={editor.focus.registerMoveButton(step.id, 1)}
          disabled={disabled || index === count - 1}
          onClick={() => {
            editor.move(step.id, 1);
          }}
        >
          <ArrowDown aria-hidden className="size-5" />
        </Button>
        <Button
          variant="danger"
          className="min-w-11 px-0"
          aria-label={t('steps.remove', { number })}
          disabled={disabled || count === 1}
          onClick={() => {
            editor.remove(step.id);
          }}
        >
          <Trash2 aria-hidden className="size-5" />
        </Button>
      </div>
    </li>
  );
}
