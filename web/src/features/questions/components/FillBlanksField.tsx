import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { questionBlanksMax } from '../api/questionOptions';
import { nextBlankId } from '../api/questionValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { NormalizationRulesField } from './NormalizationRulesField';
import { TextAreaField } from './TextAreaField';

export function FillBlanksField() {
  const { t } = useTranslation('questions');
  const { fields, append, remove } = useFieldArray<QuestionValues, 'blanks'>({ name: 'blanks' });
  const blanks = useWatch<QuestionValues, 'blanks'>({ name: 'blanks' });
  const { errors } = useFormState<QuestionValues>({ name: 'blanks' });
  const message = errors.blanks?.message ?? errors.blanks?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.blanks.legend')}</legend>
      {fields.map((field, index) => (
        <div key={field.id} className="flex items-start gap-2.5">
          <code dir="ltr" className="mt-8 font-mono text-mono">
            [[{blanks[index]?.id}]]
          </code>
          <div className="min-w-0 flex-1">
            <TextAreaField<QuestionValues>
              name={`blanks.${index.toString()}.acceptedAnswers` as `blanks.${number}.acceptedAnswers`}
              label={t('editor.blanks.accepted', { number: index + 1 })}
            />
          </div>
          <Button
            variant="ghost"
            size="sm"
            className="mt-6"
            aria-label={t('editor.blanks.remove', { number: index + 1 })}
            disabled={fields.length <= 1}
            onClick={() => {
              remove(index);
            }}
          >
            <Trash2 aria-hidden className="size-4" />
          </Button>
        </div>
      ))}
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
      <div>
        <Button
          variant="secondary"
          size="sm"
          disabled={fields.length >= questionBlanksMax}
          onClick={() => {
            append({ id: nextBlankId(blanks.map((blank) => blank.id)), acceptedAnswers: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.blanks.add')}
        </Button>
      </div>
      <NormalizationRulesField />
    </fieldset>
  );
}
