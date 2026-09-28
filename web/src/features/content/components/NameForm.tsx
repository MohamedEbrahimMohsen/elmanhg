import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Button } from '@/shared/ui/button';
import { nameSchema, type NameValues } from '../schemas/nameSchema';

export interface NameFormProps {
  label: string;
  submitLabel: string;
  defaultName?: string;
  serverErrorFields: ServerErrorFields<NameValues>;
  onSubmit: (name: string) => Promise<void>;
  onCancel?: () => void;
}

export function NameForm({ label, submitLabel, defaultName, serverErrorFields, onSubmit, onCancel }: NameFormProps) {
  const { t } = useTranslation('content');
  const form = useForm<NameValues>({ resolver: zodResolver(nameSchema), defaultValues: { name: defaultName ?? '' } });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        await onSubmit(values.name);
        if (defaultName === undefined) {
          form.reset({ name: '' });
        }
      }}
    >
      <FormRootError />
      <TextField<NameValues> name="name" label={label} />
      <div className="flex flex-wrap gap-2">
        <SubmitButton>{submitLabel}</SubmitButton>
        {onCancel ? (
          <Button variant="secondary" onClick={onCancel}>
            {t('actions.cancel')}
          </Button>
        ) : null}
      </div>
    </Form>
  );
}
