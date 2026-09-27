import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { devSessions } from '../devSessions';
import { useSessionStore } from '../hooks/useSession';
import { roles } from '../sessionStore';

export function DevSignInPage() {
  const { t } = useTranslation('session');
  const store = useSessionStore();

  return (
    <main id="main" className="mx-auto flex min-h-dvh max-w-layout flex-col justify-center gap-6 px-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('signIn.title')}</h1>
      <p className="text-caption text-text-muted">{t('signIn.devNotice')}</p>
      <div className="flex flex-col gap-3">
        {roles.map((role) => (
          <Button
            key={role}
            variant={role === 'student' ? 'primary' : 'secondary'}
            onClick={() => {
              store.set(devSessions[role]);
            }}
          >
            {t(`signIn.as.${role}`)}
          </Button>
        ))}
      </div>
    </main>
  );
}
