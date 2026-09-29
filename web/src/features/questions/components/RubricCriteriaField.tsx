import { Plus } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { emptyCriterion, nextCriterionId, rubricTotalPoints } from '../api/essayValues';
import { rubricCriteriaMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { RubricCriterionCard } from './RubricCriterionCard';

export function RubricCriteriaField() {
  const { t } = useTranslation('questions');
  const { fields, append, remove } = useFieldArray<QuestionValues, 'criteria'>({ name: 'criteria' });
  const criteria = useWatch<QuestionValues, 'criteria'>({ name: 'criteria' });
  const { errors } = useFormState<QuestionValues>({ name: 'criteria' });
  const message = errors.criteria?.message ?? errors.criteria?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.essay.rubricLegend')}</legend>
      <p className="text-caption text-text-muted">
        {t('editor.essay.rubricHint', { total: rubricTotalPoints(criteria) })}
      </p>
      {fields.map((field, index) => (
        <RubricCriterionCard
          key={field.id}
          index={index}
          canRemove={fields.length > 1}
          onRemove={() => {
            remove(index);
          }}
        />
      ))}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <div>
        <Button
          variant="secondary"
          size="sm"
          disabled={fields.length >= rubricCriteriaMax}
          onClick={() => {
            append(emptyCriterion(nextCriterionId(criteria.map((criterion) => criterion.id))));
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.essay.addCriterion')}
        </Button>
      </div>
    </fieldset>
  );
}
