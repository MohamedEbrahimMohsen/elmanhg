import { useMatch } from '@tanstack/react-router';
import { Sparkles } from 'lucide-react';
import { lazy, Suspense, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvatar } from '../hooks/useAvatar';

const AvatarPanel = lazy(() => import('./AvatarPanel').then((module) => ({ default: module.AvatarPanel })));

const dockClassName =
  'fixed start-4 bottom-24 z-10 inline-flex h-12 items-center gap-2 rounded-pill bg-accent px-5 text-ui font-semibold text-surface shadow-2 hover:bg-accent-hover active:bg-accent-pressed focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden lg:bottom-6';

export function AvatarDock() {
  const { t } = useTranslation('avatar');
  const { state, open } = useAvatar();
  const takingExam = useMatch({ from: '/student/exam/$sessionId', shouldThrow: false }) !== undefined;
  const [mounted, setMounted] = useState(state.isOpen);
  if (state.isOpen && !mounted) {
    setMounted(true);
  }

  return (
    <>
      {state.isOpen || takingExam ? null : (
        <button
          type="button"
          onClick={() => {
            open({ entryPoint: 'Global' });
          }}
          className={dockClassName}
        >
          <Sparkles aria-hidden strokeWidth={1.8} className="size-4" />
          {t('dock.open')}
        </button>
      )}
      {mounted ? (
        <Suspense
          fallback={
            <button type="button" disabled aria-busy="true" className={dockClassName}>
              <Sparkles aria-hidden strokeWidth={1.8} className="size-4" />
              {t('dock.open')}
            </button>
          }
        >
          <AvatarPanel />
        </Suspense>
      ) : null}
    </>
  );
}
