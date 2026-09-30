import { useEffect, useRef, type RefCallback } from 'react';
import type { MathFieldId, MoveDirection } from '../api/mathStepsValue';

export type MathFocusTarget =
  { kind: 'field'; field: MathFieldId; caret?: number } | { kind: 'move'; stepId: string; direction: MoveDirection };

export interface MathFieldFocus {
  registerField: (field: MathFieldId) => RefCallback<HTMLTextAreaElement>;
  registerMoveButton: (stepId: string, direction: MoveDirection) => RefCallback<HTMLButtonElement>;
  fieldElement: (field: MathFieldId) => HTMLTextAreaElement | undefined;
  focusNow: (target: MathFocusTarget) => void;
  focusAfterRender: (target: MathFocusTarget) => void;
}

const buttonKey = (stepId: string, direction: MoveDirection) => `${stepId}:${String(direction)}`;

export function useMathFieldFocus(): MathFieldFocus {
  const fields = useRef(new Map<MathFieldId, HTMLTextAreaElement>());
  const buttons = useRef(new Map<string, HTMLButtonElement>());
  const pending = useRef<MathFocusTarget | null>(null);

  const focusNow = (target: MathFocusTarget): void => {
    if (target.kind === 'field') {
      const element = fields.current.get(target.field);
      element?.focus();
      if (target.caret !== undefined) {
        element?.setSelectionRange(target.caret, target.caret);
      }
      return;
    }
    const preferred = buttons.current.get(buttonKey(target.stepId, target.direction));
    const opposite: MoveDirection = target.direction === 1 ? -1 : 1;
    const button =
      preferred && !preferred.disabled ? preferred : buttons.current.get(buttonKey(target.stepId, opposite));
    button?.focus();
  };

  useEffect(() => {
    const target = pending.current;
    if (target === null) {
      return;
    }
    pending.current = null;
    focusNow(target);
  });

  return {
    registerField: (field) => (element) => {
      if (element === null) {
        return;
      }
      fields.current.set(field, element);
      return () => {
        fields.current.delete(field);
      };
    },
    registerMoveButton: (stepId, direction) => (element) => {
      if (element === null) {
        return;
      }
      buttons.current.set(buttonKey(stepId, direction), element);
      return () => {
        buttons.current.delete(buttonKey(stepId, direction));
      };
    },
    fieldElement: (field) => fields.current.get(field),
    focusNow,
    focusAfterRender: (target) => {
      pending.current = target;
    },
  };
}
