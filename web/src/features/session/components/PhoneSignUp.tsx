import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { trackFunnelEvent } from '@/features/analytics';
import { useRegisterWithPhone } from '@/shared/api/generated/auth/auth';
import { useStartSession } from '../hooks/useStartSession';
import { OtpForm } from './OtpForm';
import { PhoneStartForm, type PhoneStart } from './PhoneStartForm';

export function PhoneSignUp() {
  const { t } = useTranslation('session');
  const [start, setStart] = useState<PhoneStart | null>(null);
  const registerWithPhone = useRegisterWithPhone();
  const startSession = useStartSession();

  if (start === null) {
    return <PhoneStartForm withDisplayName onCodeSent={setStart} />;
  }

  return (
    <OtpForm
      recipient={start.phoneNumber}
      request={{ phoneNumber: start.phoneNumber }}
      delivery={start.delivery}
      changeLabel={t('actions.changeNumber')}
      onVerified={async (verificationId) => {
        const result = await registerWithPhone.mutateAsync({
          data: { verificationId, displayName: start.displayName },
        });
        trackFunnelEvent('SignUpCompleted');
        startSession(result);
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
