import { useFormContext, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { TextField } from '@/shared/form/TextField';
import type { ExamBlueprintValues } from '../schemas/examBlueprintSchema';

export function DifficultyMixFields() {
  const { t } = useTranslation('blueprints');
  const {
    register,
    control,
    formState: { errors },
  } = useFormContext<ExamBlueprintValues>();
  const enabled = useWatch({ control, name: 'difficultyMix.enabled' });
  const message = errors.difficultyMix?.message;

  return (
    <div className="flex flex-col gap-2">
      <label className="flex min-h-11 items-center gap-2.5 text-ui">
        <input type="checkbox" className="size-4.5 accent-text" {...register('difficultyMix.enabled')} />
        {t('editor.mixToggle')}
      </label>
      {enabled ? (
        <div className="grid gap-3 md:grid-cols-3">
          <TextField<ExamBlueprintValues> name="difficultyMix.easy" label={t('editor.mixEasy')} dir="ltr" />
          <TextField<ExamBlueprintValues> name="difficultyMix.medium" label={t('editor.mixMedium')} dir="ltr" />
          <TextField<ExamBlueprintValues> name="difficultyMix.hard" label={t('editor.mixHard')} dir="ltr" />
        </div>
      ) : null}
      {message ? <p className="text-caption text-danger">{t(message)}</p> : null}
    </div>
  );
}
