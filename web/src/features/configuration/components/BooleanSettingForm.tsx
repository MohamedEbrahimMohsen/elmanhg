import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { readSettingValue } from '../api/runtimeSettingValue';
import { settingErrorMessage, type RuntimeSettingMutations } from '../hooks/useRuntimeSettingMutations';
import { booleanSettingSchema, type BooleanSettingValues } from '../schemas/runtimeSettingSchemas';

export interface BooleanSettingFormProps {
  setting: RuntimeSettingResult;
  mutations: RuntimeSettingMutations;
}

export function BooleanSettingForm({ setting, mutations }: BooleanSettingFormProps) {
  const { t } = useTranslation('configuration');
  const id = useId();
  const errorId = `${id}-error`;
  const form = useForm<BooleanSettingValues>({
    resolver: zodResolver(booleanSettingSchema),
    defaultValues: { value: readSettingValue(setting, setting.value) === true },
  });
  const error = form.formState.errors.value;

  const submit = async ({ value }: BooleanSettingValues) => {
    try {
      await mutations.save(value);
    } catch (caught) {
      form.setError('value', { type: 'server', message: settingErrorMessage(caught) }, { shouldFocus: true });
    }
  };

  return (
    <form
      noValidate
      className="flex flex-wrap items-center gap-3"
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
    >
      <div className="flex items-center gap-2">
        <input
          id={id}
          type="checkbox"
          className="size-6 accent-text"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          {...form.register('value')}
        />
        <label htmlFor={id} className="text-ui text-text">
          {t('row.enabled')}
        </label>
      </div>
      <Button type="submit" size="sm" disabled={mutations.isPending || form.formState.isSubmitting}>
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
