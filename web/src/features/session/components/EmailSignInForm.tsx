import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useLoginWithEmail } from '@/shared/api/generated/auth/auth';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { useStartSession } from '../hooks/useStartSession';
import { emailSignInSchema, type EmailSignInValues } from '../schemas/emailSignInSchema';

export function EmailSignInForm() {
  const { t } = useTranslation('session');
  const loginWithEmail = useLoginWithEmail();
  const startSession = useStartSession();
  const form = useForm<EmailSignInValues>({
    resolver: zodResolver(emailSignInSchema),
    defaultValues: { email: '', password: '' },
  });

  return (
    <Form
      form={form}
      onSubmit={async (values) => {
        startSession(await loginWithEmail.mutateAsync({ data: values }));
      }}
    >
      <FormRootError />
      <TextField<EmailSignInValues> name="email" label={t('fields.email')} type="email" autoComplete="email" />
      <TextField<EmailSignInValues>
        name="password"
        label={t('fields.password')}
        type="password"
        autoComplete="current-password"
      />
      <SubmitButton>{t('actions.signIn')}</SubmitButton>
    </Form>
  );
}
