import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AuthLayout } from '../components/AuthLayout';
import { EmailCodeStartForm, type EmailCodeStart } from '../components/EmailCodeStartForm';
import { OtpForm } from '../components/OtpForm';
import { SetPasswordForm } from '../components/SetPasswordForm';

export function AcceptInvitePage() {
  const { t } = useTranslation('session');
  const [start, setStart] = useState<EmailCodeStart | null>(null);
  const [verificationId, setVerificationId] = useState<string | null>(null);

  const renderStep = () => {
    if (verificationId !== null) {
      return <SetPasswordForm verificationId={verificationId} />;
    }
    if (start === null) {
      return <EmailCodeStartForm onCodeSent={setStart} />;
    }
    return (
      <OtpForm
        recipient={start.email}
        request={{ phoneNumber: null, email: start.email }}
        delivery={start.delivery}
        changeLabel={t('actions.changeEmail')}
        onVerified={(id) => {
          setVerificationId(id);
          return Promise.resolve();
        }}
        onResent={(delivery) => {
          setStart({ ...start, delivery });
        }}
        onChange={() => {
          setStart(null);
        }}
      />
    );
  };

  return (
    <AuthLayout
      title={t('acceptInvite.title')}
      footer={
        <>
          {t('acceptInvite.haveAccount')}{' '}
          <Link
            to="/login"
            className="font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {t('acceptInvite.signIn')}
          </Link>
        </>
      }
    >
      {verificationId === null ? <p className="text-ui text-text">{t('acceptInvite.intro')}</p> : null}
      {renderStep()}
    </AuthLayout>
  );
}
