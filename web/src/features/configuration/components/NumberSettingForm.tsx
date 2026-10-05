import { useId } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { formatSettingNumber, settingBound } from '../api/runtimeSettingValue';
import { settingErrorMessage, type RuntimeSettingMutations } from '../hooks/useRuntimeSettingMutations';
import { numberSettingSchema, type NumberSettingValues } from '../schemas/runtimeSettingSchemas';

export interface NumberSettingFormProps {
  setting: RuntimeSettingResult;
  mutations: RuntimeSettingMutations;
}

export function NumberSettingForm({ setting, mutations }: NumberSettingFormProps) {
  const { t, i18n } = useTranslation('configuration');
  const id = useId();
  const errorId = `${id}-error`;
  const form = useForm<NumberSettingValues>({
    resolver: zodResolver(numberSettingSchema(setting)),
    defaultValues: { value: '' },
  });
  const error = form.formState.errors.value;
  const bound = (value: number | string | null) => {
    const number = settingBound(value);
    return number === null ? '' : formatSettingNumber(number, i18n.language);
  };

  const submit = async ({ value }: NumberSettingValues) => {
    try {
      await mutations.save(Number(value));
      form.reset({ value: '' });
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
        <Input
          id={id}
          type="text"
          dir="ltr"
          inputMode={setting.type === 'Integer' ? 'numeric' : 'decimal'}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          {...form.register('value')}
        />
      </div>
      <Button type="submit" variant="secondary" disabled={mutations.isPending || form.formState.isSubmitting}>
        {t('row.save')}
      </Button>
      {error ? (
        <p id={errorId} className="w-full text-caption text-danger">
          {t([error.message ?? '', 'common:errors.UNHANDLED_EXCEPTION'], {
            min: bound(setting.minimum),
            max: bound(setting.maximum),
          })}
        </p>
      ) : null}
    </form>
  );
}
