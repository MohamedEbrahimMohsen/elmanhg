import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { EmailCodeSignIn } from './EmailCodeSignIn';
import { EmailSignInForm } from './EmailSignInForm';

export function EmailSignIn() {
  const { t } = useTranslation('session');
  const [mode, setMode] = useState<'password' | 'code'>('password');

  return (
    <div className="flex flex-col gap-4">
      {mode === 'password' ? <EmailSignInForm /> : <EmailCodeSignIn />}
      <Button
        type="button"
        variant="ghost"
        onClick={() => {
          setMode(mode === 'password' ? 'code' : 'password');
        }}
      >
        {t(mode === 'password' ? 'actions.useEmailCode' : 'actions.usePassword')}
      </Button>
    </div>
  );
}
