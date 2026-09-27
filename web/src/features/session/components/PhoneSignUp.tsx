import { useState } from 'react';
import { useRegisterWithPhone } from '@/shared/api/generated/auth/auth';
import { useStartSession } from '../hooks/useStartSession';
import { OtpForm } from './OtpForm';
import { PhoneStartForm, type PhoneStart } from './PhoneStartForm';

export function PhoneSignUp() {
  const [start, setStart] = useState<PhoneStart | null>(null);
  const registerWithPhone = useRegisterWithPhone();
  const startSession = useStartSession();

  if (start === null) {
    return <PhoneStartForm withDisplayName onCodeSent={setStart} />;
  }

  return (
    <OtpForm
      start={start}
      onVerified={async (verificationId) => {
        startSession(await registerWithPhone.mutateAsync({ data: { verificationId, displayName: start.displayName } }));
      }}
      onResent={(verificationId) => {
        setStart({ ...start, verificationId });
      }}
      onChangeNumber={() => {
        setStart(null);
      }}
    />
  );
}
