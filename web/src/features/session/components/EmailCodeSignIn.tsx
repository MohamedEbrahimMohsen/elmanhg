import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLoginWithEmailCode } from '@/shared/api/generated/auth/auth';
import { useStartSession } from '../hooks/useStartSession';
import { EmailCodeStartForm, type EmailCodeStart } from './EmailCodeStartForm';
import { OtpForm } from './OtpForm';

export function EmailCodeSignIn() {
  const { t } = useTranslation('session');
  const [start, setStart] = useState<EmailCodeStart | null>(null);
  const loginWithEmailCode = useLoginWithEmailCode();
  const startSession = useStartSession();

  if (start === null) {
    return <EmailCodeStartForm onCodeSent={setStart} />;
  }

  return (
    <OtpForm
      recipient={start.email}
      request={{ phoneNumber: null, email: start.email }}
      delivery={start.delivery}
      changeLabel={t('actions.changeEmail')}
      onVerified={async (verificationId) => {
        startSession(await loginWithEmailCode.mutateAsync({ data: { verificationId } }));
      }}
      onResent={(delivery) => {
        setStart({ ...start, delivery });
      }}
      onChange={() => {
        setStart(null);
      }}
    />
  );
}
