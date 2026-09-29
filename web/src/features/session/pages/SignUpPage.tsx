import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useFunnelEventOnMount } from '@/features/analytics';
import { AuthLayout } from '../components/AuthLayout';
import { EmailSignUpForm } from '../components/EmailSignUpForm';
import { MethodSwitch, type SignInMethod } from '../components/MethodSwitch';
import { PhoneSignUp } from '../components/PhoneSignUp';

export function SignUpPage() {
  const { t } = useTranslation('session');
  const [method, setMethod] = useState<SignInMethod>('phone');
  useFunnelEventOnMount('SignUpStarted');

  return (
    <AuthLayout
      title={t('signUp.title')}
      footer={
        <>
          {t('signUp.haveAccount')}{' '}
          <Link
            to="/login"
            className="font-semibold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          >
            {t('signUp.signIn')}
          </Link>
        </>
      }
    >
      <MethodSwitch value={method} onChange={setMethod} />
      {method === 'phone' ? <PhoneSignUp /> : <EmailSignUpForm />}
    </AuthLayout>
  );
}
