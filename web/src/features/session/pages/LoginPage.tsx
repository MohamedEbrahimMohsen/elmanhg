import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AuthLayout } from '../components/AuthLayout';
import { EmailSignIn } from '../components/EmailSignIn';
import { MethodSwitch, type SignInMethod } from '../components/MethodSwitch';
import { PhoneSignIn } from '../components/PhoneSignIn';

export function LoginPage() {
  const { t } = useTranslation('session');
  const [method, setMethod] = useState<SignInMethod>('phone');

  return (
    <AuthLayout
      title={t('signIn.title')}
      footer={
        <>
          {t('signIn.noAccount')}{' '}
          <Link
            to="/signup"
            className="font-semibold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {t('signIn.createAccount')}
          </Link>
        </>
      }
    >
      <MethodSwitch value={method} onChange={setMethod} />
      {method === 'phone' ? <PhoneSignIn /> : <EmailSignIn />}
    </AuthLayout>
  );
}
