import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { FormProvider, useForm, useWatch, type UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { GradeReviewDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { gradeReviewCommentMaxLength } from '../api/gradeReviewOptions';
import { gradeReviewFormSchema, type GradeReviewFormValues } from '../schemas/gradeReviewFormSchema';
import { GradeReviewDecisionField } from './GradeReviewDecisionField';

export interface GradeReviewFormProps {
  detail: GradeReviewDetailResult;
  onSubmit: (values: GradeReviewFormValues, form: UseFormReturn<GradeReviewFormValues>) => Promise<void>;
  isPending: boolean;
}

const inputClassName =
  'w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger';

export function GradeReviewForm({ detail, onSubmit, isPending }: GradeReviewFormProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const maxScoreNumber = Number(detail.maxScore);
  const maxScore = formatNumber(maxScoreNumber, lng, 'latin');
  const form = useForm<GradeReviewFormValues>({
    resolver: zodResolver(gradeReviewFormSchema(maxScoreNumber)),
    defaultValues: { decision: detail.aiScore === null ? 'Overridden' : 'Accepted', score: '', comment: '' },
    mode: 'onBlur',
  });
  const id = useId();
  const decision = useWatch({ control: form.control, name: 'decision' });
  const { errors } = form.formState;
  const scoreErrorId = `${id}-score-error`;
  const commentHintId = `${id}-comment-hint`;
  const commentErrorId = `${id}-comment-error`;
  const aiScoreLabel = detail.aiScore === null ? null : formatNumber(Number(detail.aiScore), lng, 'latin');

  return (
    <FormProvider {...form}>
      <form
        noValidate
        aria-label={t('form.decision')}
        className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
        onSubmit={(event) => {
          void form.handleSubmit((values) => onSubmit(values, form))(event);
        }}
      >
        <GradeReviewDecisionField aiScoreLabel={aiScoreLabel} maxScore={maxScore} />
        {decision === 'Overridden' ? (
          <div className="flex flex-col gap-1.5">
            <label htmlFor={`${id}-score`} className="text-ui font-semibold text-text">
              {t('form.score', { maxScore })}
            </label>
            <input
              id={`${id}-score`}
              type="text"
              inputMode="decimal"
              dir="ltr"
              aria-invalid={errors.score ? true : undefined}
              aria-describedby={errors.score ? scoreErrorId : undefined}
              className={inputClassName}
              {...form.register('score')}
            />
            {errors.score?.message ? (
              <p id={scoreErrorId} className="text-caption text-danger">
                {t(errors.score.message)}
              </p>
            ) : null}
          </div>
        ) : null}
        <div className="flex flex-col gap-1.5">
          <label htmlFor={`${id}-comment`} className="text-ui font-semibold text-text">
            {t(decision === 'Overridden' ? 'form.commentRequiredLabel' : 'form.commentOptionalLabel')}
          </label>
          <textarea
            id={`${id}-comment`}
            rows={3}
            dir="auto"
            aria-invalid={errors.comment ? true : undefined}
            aria-describedby={errors.comment ? `${commentHintId} ${commentErrorId}` : commentHintId}
            className={`min-h-20 ${inputClassName}`}
            {...form.register('comment')}
          />
          <p id={commentHintId} className="text-caption text-text-muted">
            {t('form.commentHint', { max: formatNumber(gradeReviewCommentMaxLength, lng, 'latin') })}
          </p>
          {errors.comment?.message ? (
            <p id={commentErrorId} className="text-caption text-danger">
              {t(errors.comment.message)}
            </p>
          ) : null}
        </div>
        <Button variant="primary" type="submit" disabled={isPending} className="self-start">
          {t('form.submit')}
        </Button>
      </form>
    </FormProvider>
  );
}
