import { useState } from 'react';
import { Keyboard, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { mathStepsMaxCount, stepFieldId, type MathFieldId, type MathStepsValue } from '../api/mathStepsValue';
import { useMathStepsEditor } from '../hooks/useMathStepsEditor';
import { MathField } from './MathField';
import { MathKeypad } from './MathKeypad';
import { MathStepRow } from './MathStepRow';

export interface MathStepsInputProps {
  value: MathStepsValue;
  onChange: (value: MathStepsValue) => void;
  disabled?: boolean | undefined;
}

export function MathStepsInput({ value, onChange, disabled = false }: MathStepsInputProps) {
  const { t } = useTranslation('mathSteps');
  const editor = useMathStepsEditor({ value, onChange });
  const [keypadOpen, setKeypadOpen] = useState(true);
  const atMax = value.steps.length >= mathStepsMaxCount;
  const keypadFor = (field: MathFieldId) =>
    keypadOpen && !disabled && editor.activeField === field ? <MathKeypad onKey={editor.pressKey} /> : null;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-start justify-between gap-3">
        <p className="text-caption text-text-muted">{t('input.hint')}</p>
        <Button
          variant="secondary"
          size="sm"
          aria-pressed={keypadOpen}
          disabled={disabled}
          onClick={() => {
            setKeypadOpen(!keypadOpen);
          }}
        >
          <Keyboard aria-hidden className="size-5" />
          {t('keypad.toggle')}
        </Button>
      </div>
      <fieldset className="flex flex-col gap-3">
        <legend className="font-display text-h3 font-semibold text-text">{t('steps.title')}</legend>
        <ol className="flex flex-col gap-3">
          {value.steps.map((step, index) => (
            <MathStepRow
              key={step.id}
              step={step}
              index={index}
              count={value.steps.length}
              disabled={disabled}
              keypadOpen={keypadOpen}
              editor={editor}
              keypad={keypadFor(stepFieldId(step.id))}
            />
          ))}
        </ol>
      </fieldset>
      <Button variant="secondary" disabled={disabled || atMax} onClick={editor.add}>
        <Plus aria-hidden className="size-5" />
        {t('steps.add')}
      </Button>
      {atMax && <p className="text-caption text-text-muted">{t('steps.max', { max: mathStepsMaxCount })}</p>}
      <MathField
        field="final"
        label={t('final.label')}
        value={value.finalAnswer}
        rows={1}
        disabled={disabled}
        keypadOpen={keypadOpen}
        editor={editor}
      />
      {keypadFor('final')}
      <p role="status" className="sr-only">
        {editor.announcement}
      </p>
    </div>
  );
}
