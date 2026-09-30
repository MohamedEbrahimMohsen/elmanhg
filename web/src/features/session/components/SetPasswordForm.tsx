import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { useAcceptInvitation } from '@/shared/api/generated/auth/auth';
import type { ServerErrorFields } from '@/shared/form/applyServerErrors';
import { Form } from '@/shared/form/Form';
import { FormRootError } from '@/shared/form/FormRootError';
import { SubmitButton } from '@/shared/form/SubmitButton';
import { TextField } from '@/shared/form/TextField';
import { useStartSession } from '../hooks/useStartSession';
import { setPasswordSchema, type SetPasswordValues } from '../schemas/setPasswordSchema';

export interface SetPasswordFormProps {
  verificationId: string;
}

const serverErrorFields: ServerErrorFields<SetPasswordValues> = {
  PASSWORD_TOO_SHORT: 'password',
  PASSWORD_MUST_CONTAIN_DIGIT: 'password',
  PASSWORD_REJECTED: 'password',
};

export function SetPasswordForm({ verificationId }: SetPasswordFormProps) {
  const { t } = useTranslation('session');
  const acceptInvitation = useAcceptInvitation();
  const startSession = useStartSession();
  const form = useForm<SetPasswordValues>({
    resolver: zodResolver(setPasswordSchema),
    defaultValues: { password: '', confirmPassword: '' },
  });

  return (
    <Form
      form={form}
      serverErrorFields={serverErrorFields}
      onSubmit={async (values) => {
        startSession(await acceptInvitation.mutateAsync({ data: { verificationId, password: values.password } }));
      }}
    >
      <p className="text-ui text-text">{t('acceptInvite.passwordIntro')}</p>
      <FormRootError />
      <TextField<SetPasswordValues>
        name="password"
        label={t('fields.newPassword')}
        type="password"
        autoComplete="new-password"
        description={t('fields.passwordHint')}
      />
      <TextField<SetPasswordValues>
        name="confirmPassword"
        label={t('fields.confirmPassword')}
        type="password"
        autoComplete="new-password"
      />
      <SubmitButton>{t('actions.setPassword')}</SubmitButton>
    </Form>
  );
}
