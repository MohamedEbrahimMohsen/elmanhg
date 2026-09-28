import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useLoginWithPhone } from '@/shared/api/generated/auth/auth';
import { useStartSession } from '../hooks/useStartSession';
import { OtpForm } from './OtpForm';
import { PhoneStartForm, type PhoneStart } from './PhoneStartForm';

export function PhoneSignIn() {
  const { t } = useTranslation('session');
  const [start, setStart] = useState<PhoneStart | null>(null);
  const loginWithPhone = useLoginWithPhone();
  const startSession = useStartSession();

  if (start === null) {
    return <PhoneStartForm withDisplayName={false} onCodeSent={setStart} />;
  }

  return (
    <OtpForm
      recipient={start.phoneNumber}
      request={{ phoneNumber: start.phoneNumber }}
      delivery={start.delivery}
      changeLabel={t('actions.changeNumber')}
      onVerified={async (verificationId) => {
        startSession(await loginWithPhone.mutateAsync({ data: { verificationId } }));
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
