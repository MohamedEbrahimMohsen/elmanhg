import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Label } from '@/shared/ui/label';
import { readSettingValue } from '../api/runtimeSettingValue';
import { settingErrorMessage, type RuntimeSettingMutations } from '../hooks/useRuntimeSettingMutations';
import { choiceSettingSchema, type ChoiceSettingValues } from '../schemas/runtimeSettingSchemas';

export interface ChoiceSettingFormProps {
  setting: RuntimeSettingResult;
  mutations: RuntimeSettingMutations;
}

export function ChoiceSettingForm({ setting, mutations }: ChoiceSettingFormProps) {
  const { t } = useTranslation('configuration');
  const id = useId();
  const errorId = `${id}-error`;
  const current = readSettingValue(setting, setting.value);
  const form = useForm<ChoiceSettingValues>({
    resolver: zodResolver(choiceSettingSchema(setting)),
    defaultValues: { value: typeof current === 'string' ? current : '' },
  });
  const error = form.formState.errors.value;

  const submit = async ({ value }: ChoiceSettingValues) => {
    try {
      await mutations.save(value);
    } catch (caught) {
      form.setError('value', { type: 'server', message: settingErrorMessage(caught) }, { shouldFocus: true });
    }
  };

  return (
    <form
      noValidate
      className="flex flex-wrap items-end gap-2"
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor={id}>{t('row.newValue')}</Label>
        <select
          id={id}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className="h-11 w-full rounded-sm border border-border-strong bg-surface px-3 text-ui text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          {...form.register('value')}
        >
          {setting.allowedValues.map((option) => (
            <option key={option} value={option}>
              {t([`choices.${option}`, option])}
            </option>
          ))}
        </select>
      </div>
      <Button type="submit" disabled={mutations.isPending || form.formState.isSubmitting}>
        {t('row.save')}
      </Button>
      {error ? (
        <p id={errorId} className="w-full text-caption text-danger">
          {t([error.message ?? '', 'common:errors.UNHANDLED_EXCEPTION'])}
        </p>
      ) : null}
    </form>
  );
}
