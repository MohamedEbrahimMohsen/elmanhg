import { Plus, Trash2 } from 'lucide-react';
import { useFieldArray, useFormState } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import { Button } from '@/shared/ui/button';
import { rubricLevelsMax, rubricLevelsMin } from '../api/questionOptions';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface RubricLevelsFieldProps {
  criterionIndex: number;
}

export function RubricLevelsField({ criterionIndex }: RubricLevelsFieldProps) {
  const { t } = useTranslation('questions');
  const name = `criteria.${criterionIndex.toString()}.levels` as `criteria.${number}.levels`;
  const { fields, append, remove } = useFieldArray<QuestionValues, `criteria.${number}.levels`>({ name });
  const { errors } = useFormState<QuestionValues>({ name });
  const levelErrors = errors.criteria?.[criterionIndex]?.levels;
  const message = levelErrors?.message ?? levelErrors?.root?.message;
  const number = criterionIndex + 1;

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-2 text-caption text-text-muted">{t('editor.essay.levelsLegend', { number })}</legend>
      {fields.map((field, index) => {
        const level = index + 1;
        return (
          <div key={field.id} className="flex items-start gap-2">
            <TextField<QuestionValues>
              name={`${name}.${index.toString()}.points` as `criteria.${number}.levels.${number}.points`}
              label={t('editor.essay.levelPoints', { number, level })}
              dir="ltr"
            />
            <div className="min-w-0 flex-1">
              <TextField<QuestionValues>
                name={`${name}.${index.toString()}.description` as `criteria.${number}.levels.${number}.description`}
                label={t('editor.essay.levelDescription', { number, level })}
              />
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="mt-6.5"
              aria-label={t('editor.essay.removeLevel', { number, level })}
              disabled={fields.length <= rubricLevelsMin}
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
          disabled={fields.length >= rubricLevelsMax}
          onClick={() => {
            append({ points: '', description: '' });
          }}
        >
          <Plus aria-hidden className="size-4" />
          {t('editor.essay.addLevel', { number })}
        </Button>
      </div>
    </fieldset>
  );
}
