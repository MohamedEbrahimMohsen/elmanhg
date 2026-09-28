import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import type { QuestionAnswer, StudentQuestion } from '../api/studentQuestion';

export interface TextAnswerInputsProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
}

export function TextAnswerInputs({ question, answer, onAnswerChange, disabled }: TextAnswerInputsProps) {
  const { t } = useTranslation('questions');
  const id = useId();

  if (question.type === 'Fill') {
    return (
      <div className="flex flex-col gap-3">
        {question.blankIds.map((blankId, index) => (
          <div key={blankId} className="flex flex-col gap-1.5">
            <Label htmlFor={`${id}-${blankId}`}>{t('view.blank', { number: index + 1 })}</Label>
            <Input
              id={`${id}-${blankId}`}
              value={answer.blanks[blankId] ?? ''}
              disabled={disabled}
              onChange={(event) => {
                onAnswerChange({ ...answer, blanks: { ...answer.blanks, [blankId]: event.target.value } });
              }}
            />
          </div>
        ))}
      </div>
    );
  }

  const numeric = question.answerKind === 'numeric';
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('view.yourAnswer')}</Label>
      <Input
        id={id}
        value={answer.text}
        disabled={disabled}
        {...(numeric ? { dir: 'ltr', inputMode: 'decimal' as const } : {})}
        onChange={(event) => {
          onAnswerChange({ ...answer, text: event.target.value });
        }}
      />
    </div>
  );
}
