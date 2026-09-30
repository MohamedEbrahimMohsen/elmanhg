import { useState } from 'react';
import {
  fromMathStepsPayload,
  MathStepsAnswer,
  MathStepsInput,
  toMathStepsPayload,
  type MathDraftOwner,
  type MathStepsPayload,
  type MathStepsValue,
} from '@/features/mathSteps';
import type { QuestionAnswer } from '../api/studentQuestion';
import { MathStepsReadOnly } from './MathStepsReadOnly';

export interface MathStepsAnswerInputProps {
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
  draftOwner?: MathDraftOwner | undefined;
}

export function MathStepsAnswerInput({ answer, onAnswerChange, disabled, draftOwner }: MathStepsAnswerInputProps) {
  const change = (value: MathStepsValue) => {
    onAnswerChange({ ...answer, math: toMathStepsPayload(value) });
  };

  if (disabled) {
    return <MathStepsReadOnly solution={answer.math} />;
  }
  if (draftOwner) {
    return <MathStepsAnswer owner={draftOwner} initialValue={fromMathStepsPayload(answer.math)} onChange={change} />;
  }
  return <LocalMathStepsInput initial={answer.math} onChange={change} />;
}

interface LocalMathStepsInputProps {
  initial: MathStepsPayload;
  onChange: (value: MathStepsValue) => void;
}

function LocalMathStepsInput({ initial, onChange }: LocalMathStepsInputProps) {
  const [value, setValue] = useState(() => fromMathStepsPayload(initial));

  return (
    <MathStepsInput
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
    />
  );
}
