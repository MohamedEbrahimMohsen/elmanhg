import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { applyMathKey, type MathKeyId } from '../api/mathKeys';
import {
  addMathStep,
  fieldMaxLength,
  fieldValue,
  moveMathStep,
  removeMathStep,
  setFieldValue,
  stepFieldId,
  type MathFieldId,
  type MathStepsValue,
  type MoveDirection,
} from '../api/mathStepsValue';
import { useMathFieldFocus, type MathFieldFocus } from './useMathFieldFocus';

export interface MathStepsEditorOptions {
  value: MathStepsValue;
  onChange: (value: MathStepsValue) => void;
}

export interface MathStepsEditor {
  activeField: MathFieldId | null;
  announcement: string;
  focus: MathFieldFocus;
  activate: (field: MathFieldId) => void;
  changeField: (field: MathFieldId, latex: string) => void;
  add: () => void;
  remove: (stepId: string) => void;
  move: (stepId: string, direction: MoveDirection) => void;
  pressKey: (keyId: MathKeyId) => void;
}

export function useMathStepsEditor({ value, onChange }: MathStepsEditorOptions): MathStepsEditor {
  const { t } = useTranslation('mathSteps');
  const focus = useMathFieldFocus();
  const [activeField, setActiveField] = useState<MathFieldId | null>(null);
  const [announcement, setAnnouncement] = useState('');

  const add = (): void => {
    const next = addMathStep(value);
    const created = next.steps.at(-1);
    if (next === value || created === undefined) {
      return;
    }
    focus.focusAfterRender({ kind: 'field', field: stepFieldId(created.id) });
    setActiveField(stepFieldId(created.id));
    setAnnouncement(t('steps.added', { number: next.steps.length }));
    onChange(next);
  };

  const remove = (stepId: string): void => {
    const index = value.steps.findIndex((step) => step.id === stepId);
    const next = removeMathStep(value, stepId);
    const target = value.steps[index - 1] ?? next.steps[0];
    if (next === value || target === undefined) {
      return;
    }
    focus.focusAfterRender({ kind: 'field', field: stepFieldId(target.id) });
    setActiveField(stepFieldId(target.id));
    setAnnouncement(t('steps.removed', { number: index + 1 }));
    onChange(next);
  };

  const move = (stepId: string, direction: MoveDirection): void => {
    const index = value.steps.findIndex((step) => step.id === stepId);
    const next = moveMathStep(value, stepId, direction);
    if (next === value) {
      return;
    }
    focus.focusAfterRender({ kind: 'move', stepId, direction });
    setAnnouncement(t('steps.moved', { number: index + direction + 1 }));
    onChange(next);
  };

  const pressKey = (keyId: MathKeyId): void => {
    const element = activeField === null ? undefined : focus.fieldElement(activeField);
    if (activeField === null || element === undefined) {
      return;
    }
    const current = fieldValue(value, activeField);
    const state = { value: current, selectionStart: element.selectionStart, selectionEnd: element.selectionEnd };
    const result = applyMathKey(state, keyId);
    if (result.value.length > fieldMaxLength(activeField)) {
      focus.focusNow({ kind: 'field', field: activeField });
      return;
    }
    if (result.value === current) {
      focus.focusNow({ kind: 'field', field: activeField, caret: result.caret });
      return;
    }
    focus.focusAfterRender({ kind: 'field', field: activeField, caret: result.caret });
    onChange(setFieldValue(value, activeField, result.value));
  };

  return {
    activeField,
    announcement,
    focus,
    activate: setActiveField,
    changeField: (field, latex) => {
      onChange(setFieldValue(value, field, latex));
    },
    add,
    remove,
    move,
    pressKey,
  };
}
