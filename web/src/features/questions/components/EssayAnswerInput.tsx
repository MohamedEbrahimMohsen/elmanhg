import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { Label } from '@/shared/ui/label';
import { countWords, isOverWordLimit } from '../api/essayValues';
import { essayAnswerMaxLength, essayCharactersLeftShownAt } from '../api/questionOptions';
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
  const over = isOverWordLimit(question.maxWords, answer.text);
  const left = essayAnswerMaxLength - answer.text.length;

  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>{t('view.essayLabel')}</Label>
      <textarea
        id={id}
        dir="auto"
        rows={8}
        maxLength={essayAnswerMaxLength}
        spellCheck
        value={answer.text}
        placeholder={t('view.essayPlaceholder')}
        disabled={disabled}
        aria-invalid={over || undefined}
        aria-describedby={countId}
        onChange={(event) => {
          onAnswerChange({ ...answer, text: event.target.value });
        }}
        className="min-h-20 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger"
      />
      <div id={countId} className="flex flex-wrap justify-between gap-2">
        <p className={over ? 'text-caption text-danger' : 'text-caption text-text-muted'}>
          {question.maxWords
            ? t('view.essayWordsOf', { count, max: question.maxWords })
            : t('view.essayWords', { count })}
          {over && question.maxWords ? ` · ${t('view.essayOverLimit', { over: count - question.maxWords })}` : null}
        </p>
        {left <= essayCharactersLeftShownAt ? (
          <p className="text-caption text-text-muted">{t('view.essayCharactersLeft', { count: left })}</p>
        ) : null}
      </div>
    </div>
  );
}
