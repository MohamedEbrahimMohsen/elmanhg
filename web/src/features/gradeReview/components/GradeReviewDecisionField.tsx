import { useId } from 'react';
import { useFormContext } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { GradeReviewFormValues } from '../schemas/gradeReviewFormSchema';

export interface GradeReviewDecisionFieldProps {
  aiScoreLabel: string | null;
  maxScore: string;
}

const radioClassName = 'size-5 shrink-0 accent-accent';

export function GradeReviewDecisionField({ aiScoreLabel, maxScore }: GradeReviewDecisionFieldProps) {
  const { t } = useTranslation('gradeReview');
  const { register } = useFormContext<GradeReviewFormValues>();
  const hintId = useId();

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-ui font-bold text-text">{t('form.decision')}</legend>
      <label className="flex min-h-11 items-center gap-3 text-ui text-text">
        <input
          type="radio"
          value="Accepted"
          disabled={aiScoreLabel === null}
          aria-describedby={aiScoreLabel === null ? hintId : undefined}
          className={radioClassName}
          {...register('decision')}
        />
        {t('form.accept', { score: aiScoreLabel ?? '—', maxScore })}
      </label>
      {aiScoreLabel === null ? (
        <p id={hintId} className="text-caption text-text-muted">
          {t('form.acceptUnavailable')}
        </p>
      ) : null}
      <label className="flex min-h-11 items-center gap-3 text-ui text-text">
        <input type="radio" value="Overridden" className={radioClassName} {...register('decision')} />
        {t('form.override')}
      </label>
    </fieldset>
  );
}
