import { Outlet } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { Role } from '@/features/session';
import { AppBar } from './AppBar';
import { TabBar } from './TabBar';

export interface AppShellProps {
  role: Role;
}

export function AppShell({ role }: AppShellProps) {
  const { t } = useTranslation('shell');

  return (
    <div className="min-h-dvh bg-bg text-text">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-20 focus:rounded-full focus:bg-surface focus:px-4 focus:py-2 focus:shadow-2"
      >
        {t('skipToContent')}
      </a>
      <AppBar role={role} />
      <main id="main" tabIndex={-1} className="mx-auto max-w-layout px-4 pt-6 pb-24 lg:px-6 lg:pb-8">
        <Outlet />
      </main>
      <TabBar role={role} />
    </div>
  );
}
