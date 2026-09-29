import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import { countWords } from '../api/essayValues';
import type { QuestionAnswer, StudentQuestion } from '../api/studentQuestion';

export interface EssayAnswerInputProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
}

export function EssayAnswerInput({ question, answer, onAnswerChange, disabled }: EssayAnswerInputProps) {
  const { t } = useTranslation('questions');
  const id = useId();
  const countId = `${id}-count`;
  const count = countWords(answer.text);

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('view.essayLabel')}</Label>
      <textarea
        id={id}
        rows={6}
        value={answer.text}
        placeholder={t('view.essayPlaceholder')}
        disabled={disabled}
        aria-describedby={countId}
        onChange={(event) => {
          onAnswerChange({ ...answer, text: event.target.value });
        }}
        className="min-h-20 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger"
      />
      <p id={countId} className="text-caption text-text-muted">
        {question.maxWords
          ? t('view.essayWordsOf', { count, max: question.maxWords })
          : t('view.essayWords', { count })}
      </p>
    </div>
  );
}
