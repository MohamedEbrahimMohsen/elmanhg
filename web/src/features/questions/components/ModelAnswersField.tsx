import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { modelAnswersMax } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { QuestionRichTextField } from './QuestionRichTextField';

export function ModelAnswersField() {
  const { t } = useTranslation('questions');
  const { fields, append, remove } = useFieldArray<QuestionValues, 'modelAnswers'>({ name: 'modelAnswers' });
  const { errors } = useFormState<QuestionValues>({ name: 'modelAnswers' });
  const message = errors.modelAnswers?.message ?? errors.modelAnswers?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-caption text-text-muted">{t('editor.essay.modelAnswersLegend')}</legend>
      <p className="text-caption text-text-muted">{t('editor.essay.modelAnswersHint')}</p>
      {fields.map((field, index) => {
        const number = index + 1;
        return (
          <div key={field.id} className="flex items-start gap-2">
            <div className="min-w-0 flex-1">
              <QuestionRichTextField
                name={`modelAnswers.${index.toString()}.text` as `modelAnswers.${number}.text`}
                label={t('editor.essay.modelAnswer', { number })}
                compact
              />
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="mt-6.5"
              aria-label={t('editor.essay.removeModelAnswer', { number })}
              disabled={fields.length <= 1}
              onClick={() => {
                remove(index);
              }}
            >
              <Trash2 aria-hidden className="size-4" />
            </Button>
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
          disabled={fields.length >= modelAnswersMax}
          onClick={() => {
            append({ text: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.essay.addModelAnswer')}
        </Button>
      </div>
    </fieldset>
  );
}
