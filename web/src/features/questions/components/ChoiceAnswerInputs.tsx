import { useId, type ReactNode } from 'react';
import { CircleCheck, CircleX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { cn } from '@/shared/lib/utils';
import { optionCardClassName, optionCardIdleClassName } from '@/shared/ui/optionCard';
import type { ChoiceReview, QuestionAnswer, StudentQuestion } from '../api/studentQuestion';

export interface ChoiceAnswerInputsProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean | undefined;
  review?: ChoiceReview | undefined;
}

interface ChoiceItem {
  key: string;
  label: ReactNode;
  checked: boolean;
  onSelect: (checked: boolean) => void;
}

const stateClasses = {
  none: optionCardIdleClassName,
  correct: 'border-success bg-success-soft',
  wrong: 'border-danger bg-danger-soft',
} as const;

export function ChoiceAnswerInputs({ question, answer, onAnswerChange, disabled, review }: ChoiceAnswerInputsProps) {
  const { t } = useTranslation('questions');
  const name = useId();
  const multiple = question.type === 'Multi';
  const items: ChoiceItem[] =
    question.type === 'TrueFalse'
      ? [true, false].map((value) => ({
          key: String(value),
          label: value ? t('view.true') : t('view.false'),
          checked: answer.trueFalse === value,
          onSelect: () => {
            onAnswerChange({ ...answer, trueFalse: value });
          },
        }))
      : question.options.map((option) => ({
          key: option.id,
          label: <RichTextViewer html={option.text} />,
          checked: answer.optionIds.includes(option.id),
          onSelect: (checked) => {
            const optionIds = multiple
              ? checked
                ? [...answer.optionIds, option.id]
                : answer.optionIds.filter((id) => id !== option.id)
              : [option.id];
            onAnswerChange({ ...answer, optionIds });
          },
        }));

  return (
    <fieldset className="flex flex-col gap-2.5" disabled={disabled}>
      <legend className="sr-only">{t('view.answerLegend')}</legend>
      {items.map((item) => {
        const state =
          review === undefined
            ? 'none'
            : review.correctKeys.includes(item.key)
              ? 'correct'
              : item.checked
                ? 'wrong'
                : 'none';
        return (
          <label key={item.key} className={cn(optionCardClassName, stateClasses[state])}>
            <input
              type={multiple ? 'checkbox' : 'radio'}
              name={name}
              checked={item.checked}
              onChange={(event) => {
                item.onSelect(event.target.checked);
              }}
              className="size-4.5 accent-accent"
            />
            {item.label}
            {state === 'correct' ? (
              <>
                <CircleCheck aria-hidden className="ms-auto size-5 text-success-text" />
                <span className="sr-only">{t('view.correctOption')}</span>
              </>
            ) : null}
            {state === 'wrong' ? (
              <>
                <CircleX aria-hidden className="ms-auto size-5 text-danger" />
                <span className="sr-only">{t('view.wrongOption')}</span>
              </>
            ) : null}
          </label>
        );
      })}
    </fieldset>
  );
}
