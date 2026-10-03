import { Link } from '@tanstack/react-router';
import { LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { roleHome, useSession, useSignOut, type Role } from '@/features/session';
import { Button } from '@/shared/ui/button';
import { appBarClassName, appBarRowClassName, logoClassName } from '@/shared/ui/layout';
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
    <header className={appBarClassName}>
      <div className={appBarRowClassName}>
        <Link to={roleHome[role]} className={logoClassName}>
          {t('common:app.name')}
        </Link>
        <TopTabs role={role} />
        <span className="ms-auto shrink-0 rounded-pill bg-soft px-2.5 py-0.5 text-micro font-semibold text-text lg:ms-0">
          {t(`role.${role}`)}
        </span>
        <span className="max-w-28 min-w-0 truncate text-caption text-text-muted lg:max-xl:hidden xl:max-w-36">
          {session?.displayName}
        </span>
        <Button variant="secondary" size="sm" onClick={signOut} className="shrink-0 max-lg:min-h-11">
          <LogOut aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} />
          <span className="lg:max-xl:sr-only">{t('signOut')}</span>
        </Button>
      </div>
    </header>
  );
}
