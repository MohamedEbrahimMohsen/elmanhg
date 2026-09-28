import { ChevronDown, ChevronUp, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import type { LessonValues } from '../schemas/lessonSchema';

export function ObjectivesField() {
  const { t } = useTranslation('content');
  const { fields, append, move, remove } = useFieldArray<LessonValues, 'objectives'>({ name: 'objectives' });
  const { errors } = useFormState<LessonValues>({ name: 'objectives' });
  const message = errors.objectives?.message;

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-caption text-text-muted">{t('lessonEditor.fields.objectives')}</legend>
      {fields.length === 0 ? (
        <p className="text-caption text-text-muted">{t('lessonEditor.objectives.empty')}</p>
      ) : (
        <ol className="flex flex-col gap-2">
          {fields.map((field, index) => (
            <li key={field.id} className="flex flex-col gap-2">
              <TextField<LessonValues>
                name={`objectives.${index.toString()}.text` as `objectives.${number}.text`}
                label={t('lessonEditor.objectives.itemLabel', { number: index + 1 })}
              />
              <div className="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  variant="secondary"
                  aria-label={t('lessonEditor.objectives.moveUp', { number: index + 1 })}
                  disabled={index === 0}
                  onClick={() => {
                    move(index, index - 1);
                  }}
                >
                  <ChevronUp aria-hidden className="size-4" />
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  aria-label={t('lessonEditor.objectives.moveDown', { number: index + 1 })}
                  disabled={index === fields.length - 1}
                  onClick={() => {
                    move(index, index + 1);
                  }}
                >
                  <ChevronDown aria-hidden className="size-4" />
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  aria-label={t('lessonEditor.objectives.remove', { number: index + 1 })}
                  onClick={() => {
                    remove(index);
                  }}
                >
                  <Trash2 aria-hidden className="size-4" />
                </Button>
              </div>
            </li>
          ))}
        </ol>
      )}
      <Button
        variant="secondary"
        className="self-start"
        onClick={() => {
          append({ objectiveId: null, text: '' });
        }}
      >
        {t('lessonEditor.objectives.add')}
      </Button>
      {message ? (
        <p className="text-caption text-danger">{t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })}</p>
      ) : null}
    </fieldset>
  );
}
