import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { readSettingValue } from '../api/runtimeSettingValue';
import { settingErrorMessage, type RuntimeSettingMutations } from '../hooks/useRuntimeSettingMutations';
import { choiceListSettingSchema, type ChoiceListSettingValues } from '../schemas/runtimeSettingSchemas';

export interface ChoiceListSettingFormProps {
  setting: RuntimeSettingResult;
  mutations: RuntimeSettingMutations;
}

export function ChoiceListSettingForm({ setting, mutations }: ChoiceListSettingFormProps) {
  const { t } = useTranslation('configuration');
  const id = useId();
  const errorId = `${id}-error`;
  const current = readSettingValue(setting, setting.value);
  const form = useForm<ChoiceListSettingValues>({
    resolver: zodResolver(choiceListSettingSchema(setting)),
    defaultValues: { value: Array.isArray(current) ? current : [] },
  });
  const error = form.formState.errors.value;

  const submit = async ({ value }: ChoiceListSettingValues) => {
    try {
      await mutations.save(setting.allowedValues.filter((option) => value.includes(option)));
    } catch (caught) {
      form.setError('value', { type: 'server', message: settingErrorMessage(caught) });
    }
  };

  return (
    <form
      noValidate
      className="flex flex-wrap items-end gap-3"
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
    >
      <fieldset className="flex flex-wrap gap-3" aria-describedby={error ? errorId : undefined}>
        <legend className="text-caption text-text-muted">{t('row.newValue')}</legend>
        {setting.allowedValues.map((option) => (
          <label key={option} className="flex items-center gap-2 text-ui text-text">
            <input type="checkbox" value={option} className="size-6 accent-accent" {...form.register('value')} />
            {t([`choices.${option}`, option])}
          </label>
        ))}
      </fieldset>
      <Button type="submit" variant="secondary" disabled={mutations.isPending || form.formState.isSubmitting}>
        {t('row.save')}
      </Button>
      {error ? (
        <p id={errorId} role="alert" className="w-full text-caption text-danger">
          {t([error.message ?? '', 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </form>
  );
}
