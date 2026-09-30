import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import { fieldMaxLength, type MathFieldId } from '../api/mathStepsValue';
import type { MathStepsEditor } from '../hooks/useMathStepsEditor';
import { MathPreview } from './MathPreview';

export interface MathFieldProps {
  field: MathFieldId;
  label: string;
  value: string;
  disabled: boolean;
  keypadOpen: boolean;
  editor: MathStepsEditor;
  rows: number;
}

export function MathField({ field, label, value, disabled, keypadOpen, editor, rows }: MathFieldProps) {
  const { t } = useTranslation('mathSteps');
  const id = useId();

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <textarea
        id={id}
        ref={editor.focus.registerField(field)}
        dir="ltr"
        rows={rows}
        value={value}
        maxLength={fieldMaxLength(field)}
        disabled={disabled}
        spellCheck={false}
        autoCapitalize="off"
        autoCorrect="off"
        inputMode={keypadOpen ? 'none' : 'text'}
        onFocus={() => {
          editor.activate(field);
        }}
        onChange={(event) => {
          editor.changeField(field, event.target.value);
        }}
        className="min-h-11 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 font-mono text-body text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45"
      />
      <MathPreview latex={value} label={t('preview.label', { name: label })} />
    </div>
  );
}
