import { useId } from 'react';
import { Plus } from 'lucide-react';
import { useFieldArray, useFormState, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { questionOptionsMax, questionOptionsMin } from '../api/questionOptions';
import { nextOptionId } from '../api/questionValues';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { CheckboxField } from './CheckboxField';
import { ChoiceOptionRow } from './ChoiceOptionRow';

export function ChoiceOptionsField() {
  const { t } = useTranslation('questions');
  const groupName = useId();
  const { fields, append, remove } = useFieldArray<QuestionValues, 'options'>({ name: 'options' });
  const [type, options] = useWatch<QuestionValues, ['type', 'options']>({ name: ['type', 'options'] });
  const { errors } = useFormState<QuestionValues>({ name: 'options' });
  const message = errors.options?.message ?? errors.options?.root?.message;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-caption text-text-muted">{t('editor.options.legend')}</legend>
      {fields.map((field, index) => (
        <ChoiceOptionRow
          key={field.id}
          index={index}
          multiple={type === 'Multi'}
          groupName={groupName}
          canRemove={fields.length > questionOptionsMin}
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
          disabled={fields.length >= questionOptionsMax}
          onClick={() => {
            append({ id: nextOptionId(options.map((option) => option.id)), text: '', correct: false });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.options.add')}
        </Button>
      </div>
      {type === 'Multi' ? (
        <CheckboxField<QuestionValues> name="partialCredit" label={t('editor.options.partialCredit')} />
      ) : null}
    </fieldset>
  );
}
