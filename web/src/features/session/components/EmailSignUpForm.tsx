import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useRegisterWithEmail } from '@/shared/api/generated/auth/auth';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { useStartSession } from '../hooks/useStartSession';
import { emailSignUpSchema, type EmailSignUpValues } from '../schemas/emailSignUpSchema';

const serverErrorFields: ServerErrorFields<EmailSignUpValues> = {
  EMAIL_ALREADY_REGISTERED: 'email',
  EMAIL_INVALID: 'email',
  EMAIL_TOO_LONG: 'email',
  PASSWORD_TOO_SHORT: 'password',
  PASSWORD_MUST_CONTAIN_DIGIT: 'password',
  DISPLAY_NAME_TOO_LONG: 'displayName',
};

export function EmailSignUpForm() {
  const { t } = useTranslation('session');
  const registerWithEmail = useRegisterWithEmail();
  const startSession = useStartSession();
  const form = useForm<EmailSignUpValues>({
    resolver: zodResolver(emailSignUpSchema),
    defaultValues: { displayName: '', email: '', password: '' },
  });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        startSession(await registerWithEmail.mutateAsync({ data: values }));
      }}
    >
      <FormRootError />
      <TextField<EmailSignUpValues> name="displayName" label={t('fields.displayName')} autoComplete="name" />
      <TextField<EmailSignUpValues> name="email" label={t('fields.email')} type="email" autoComplete="email" />
      <TextField<EmailSignUpValues>
        name="password"
        label={t('fields.password')}
        type="password"
        autoComplete="new-password"
        description={t('fields.passwordHint')}
      />
      <SubmitButton>{t('actions.createAccount')}</SubmitButton>
    </Form>
  );
}
