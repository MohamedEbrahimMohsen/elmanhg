import { useState } from 'react';
import { useLoginWithPhone } from '@/shared/api/generated/auth/auth';
import { useStartSession } from '../hooks/useStartSession';
import { OtpForm } from './OtpForm';
import { PhoneStartForm, type PhoneStart } from './PhoneStartForm';

export function PhoneSignIn() {
  const [start, setStart] = useState<PhoneStart | null>(null);
  const loginWithPhone = useLoginWithPhone();
  const startSession = useStartSession();

  if (start === null) {
    return <PhoneStartForm withDisplayName={false} onCodeSent={setStart} />;
  }

  return (
    <OtpForm
      start={start}
      onVerified={async (verificationId) => {
        startSession(await loginWithPhone.mutateAsync({ data: { verificationId } }));
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
