import { lazy, Suspense } from 'react';
import type { MathDraftOwner } from '@/features/mathSteps';
import type { ChoiceReview, QuestionAnswer, StudentQuestion } from '../api/studentQuestion';
import { ChoiceAnswerInputs } from './ChoiceAnswerInputs';
import { EssayAnswerInput } from './EssayAnswerInput';
import { TextAnswerInputs } from './TextAnswerInputs';

const MathStepsAnswerInput = lazy(async () => {
  const module = await import('./MathStepsAnswerInput');
  return { default: module.MathStepsAnswerInput };
});

const DragDropAnswerInput = lazy(async () => {
  const module = await import('./DragDropAnswerInput');
  return { default: module.DragDropAnswerInput };
});

export interface AnswerInputsProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
  review?: ChoiceReview | undefined;
  mathDraftOwner?: MathDraftOwner | undefined;
}

export function AnswerInputs({
  question,
  answer,
  onAnswerChange,
  disabled,
  review,
  mathDraftOwner,
}: AnswerInputsProps) {
  switch (question.type) {
    case 'Mcq':
    case 'Multi':
    case 'TrueFalse':
      return (
        <ChoiceAnswerInputs
          question={question}
          answer={answer}
          onAnswerChange={onAnswerChange}
          disabled={disabled}
          review={review}
        />
      );
    case 'Fill':
    case 'Short':
      return (
        <TextAnswerInputs question={question} answer={answer} onAnswerChange={onAnswerChange} disabled={disabled} />
      );
    case 'Essay':
      return (
        <EssayAnswerInput question={question} answer={answer} onAnswerChange={onAnswerChange} disabled={disabled} />
      );
    case 'MathSteps':
      return (
        <Suspense fallback={<div aria-busy="true" className="min-h-11" />}>
          <MathStepsAnswerInput
            answer={answer}
            onAnswerChange={onAnswerChange}
            disabled={disabled}
            draftOwner={mathDraftOwner}
          />
        </Suspense>
      );
    case 'DragDrop':
      return (
        <Suspense fallback={<div aria-busy="true" className="min-h-11" />}>
          <DragDropAnswerInput
            question={question}
            answer={answer}
            onAnswerChange={onAnswerChange}
            disabled={disabled}
            review={review}
          />
        </Suspense>
      );
  }
}
