import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { MathPreview } from '@/features/mathSteps';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { mathAnswersMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export function MathAnswersField() {
  const { t } = useTranslation('questions');
  const { fields, append, remove } = useFieldArray<QuestionValues, 'mathAnswers'>({ name: 'mathAnswers' });
  const watched = useWatch<QuestionValues, 'mathAnswers'>({ name: 'mathAnswers' });
  const { errors } = useFormState<QuestionValues>({ name: 'mathAnswers' });
  const message = errors.mathAnswers?.message ?? errors.mathAnswers?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-caption text-text-muted">{t('editor.math.answersLegend')}</legend>
      <p className="text-caption text-text-muted">{t('editor.math.answersHint')}</p>
      {fields.map((field, index) => {
        const number = index + 1;
        return (
          <div key={field.id} className="flex flex-col gap-2">
            <div className="flex items-start gap-2">
              <div className="min-w-0 flex-1">
                <TextField<QuestionValues>
                  name={`mathAnswers.${index.toString()}.latex` as `mathAnswers.${number}.latex`}
                  label={t('editor.math.answer', { number })}
                  dir="ltr"
                />
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="mt-6.5"
                aria-label={t('editor.math.removeAnswer', { number })}
                disabled={fields.length <= 1}
                onClick={() => {
                  remove(index);
                }}
              >
                <Trash2 aria-hidden className="size-4" />
              </Button>
            </div>
            <MathPreview latex={watched[index]?.latex ?? ''} label={t('editor.math.answerPreview', { number })} />
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
          disabled={fields.length >= mathAnswersMax}
          onClick={() => {
            append({ latex: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.math.addAnswer')}
        </Button>
      </div>
    </fieldset>
  );
}
