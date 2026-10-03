import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { MathPreview } from '@/features/mathSteps';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { mathSolutionStepsMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export function MathSolutionField() {
  const { t } = useTranslation('questions');
  const { fields, append, remove } = useFieldArray<QuestionValues, 'mathSolution'>({ name: 'mathSolution' });
  const watched = useWatch<QuestionValues, 'mathSolution'>({ name: 'mathSolution' });
  const { errors } = useFormState<QuestionValues>({ name: 'mathSolution' });
  const message = errors.mathSolution?.message ?? errors.mathSolution?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.math.solutionLegend')}</legend>
      <p className="text-caption text-text-muted">{t('editor.math.solutionHint')}</p>
      {fields.map((field, index) => {
        const number = index + 1;
        return (
          <div key={field.id} className="flex flex-col gap-2">
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                <TextField<QuestionValues>
                  name={`mathSolution.${index.toString()}.latex` as `mathSolution.${number}.latex`}
                  label={t('editor.math.solutionStep', { number })}
                  dir="ltr"
                />
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="mt-6.5"
                aria-label={t('editor.math.removeSolutionStep', { number })}
                onClick={() => {
                  remove(index);
                }}
              >
                <Trash2 aria-hidden className="size-4" />
              </Button>
            </div>
            <MathPreview latex={watched[index]?.latex ?? ''} label={t('editor.math.solutionStepPreview', { number })} />
          </div>
        );
      })}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <div>
        <Button
          variant="secondary"
          size="sm"
          disabled={fields.length >= mathSolutionStepsMax}
          onClick={() => {
            append({ latex: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.math.addSolutionStep')}
        </Button>
      </div>
    </fieldset>
  );
}
