import { Link } from '@tanstack/react-router';
import { LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { roleHome, useSession, useSignOut, type Role } from '@/features/session';
import { Button } from '@/shared/ui/button';
import { navIconStrokeWidth } from '../navConfig';
import { TopTabs } from './TopTabs';

export interface AppBarProps {
  role: Role;
}

export function AppBar({ role }: AppBarProps) {
  const { t } = useTranslation('shell');
  const session = useSession();
  const signOut = useSignOut();

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-surface">
      <div className="mx-auto flex min-h-14 max-w-layout items-center gap-3 px-4 lg:px-6">
        <Link to={roleHome[role]} className="font-display text-h3 font-bold">
          {t('common:app.name')}
        </Link>
        <span className="rounded-full bg-text px-2.5 py-0.5 text-micro text-surface">{t(`role.${role}`)}</span>
        <span className="ms-auto text-caption text-text-muted">{session?.displayName}</span>
        <Button variant="secondary" size="sm" onClick={signOut}>
          <LogOut aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} />
          {t('signOut')}
        </Button>
      </div>
      <TopTabs role={role} />
    </header>
  );
}
