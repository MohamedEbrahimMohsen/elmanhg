import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useSendOtp } from '@/shared/api/generated/auth/auth';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { emailCodeStartSchema, type EmailCodeStartValues } from '../schemas/emailCodeStartSchema';
import type { OtpDelivery } from './OtpForm';

export interface EmailCodeStart {
  email: string;
  delivery: OtpDelivery;
}

export interface EmailCodeStartFormProps {
  onCodeSent: (start: EmailCodeStart) => void;
}

const serverErrorFields: ServerErrorFields<EmailCodeStartValues> = {
  EMAIL_REQUIRED: 'email',
  EMAIL_INVALID: 'email',
  EMAIL_TOO_LONG: 'email',
};

export function EmailCodeStartForm({ onCodeSent }: EmailCodeStartFormProps) {
  const { t } = useTranslation('session');
  const sendOtp = useSendOtp();
  const form = useForm<EmailCodeStartValues>({
    resolver: zodResolver(emailCodeStartSchema),
    defaultValues: { email: '' },
  });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        const result = await sendOtp.mutateAsync({ data: { phoneNumber: null, email: values.email } });
        onCodeSent({
          email: values.email,
          delivery: { verificationId: result.verificationId, channel: result.channel },
        });
      }}
    >
      <FormRootError />
      <TextField<EmailCodeStartValues> name="email" label={t('fields.email')} type="email" autoComplete="email" />
      <SubmitButton>{t('actions.sendCode')}</SubmitButton>
    </Form>
  );
}
