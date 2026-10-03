import { lazy, Suspense } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { roleHome, type Role } from '@/features/session';
import { navByRole, topBarItems, topBarOverflowItems } from '../navConfig';
import { topNavItemClassName } from '../navStyles';

const TopNavMore = lazy(() => import('./TopNavMore').then((m) => ({ default: m.TopNavMore })));

export interface TopTabsProps {
  role: Role;
}

export function TopTabs({ role }: TopTabsProps) {
  const { t } = useTranslation('shell');
  const nav = navByRole[role];
  const overflow = topBarOverflowItems(role, nav);

  return (
    <nav aria-label={t('nav.main')} className="hidden min-w-0 flex-1 items-center gap-1 lg:flex">
      <ul className="flex min-w-0 items-center gap-1 overflow-x-auto p-1">
        {topBarItems(role, nav).map((item) => (
          <li key={item.key} className="shrink-0">
            <Link to={item.to} activeOptions={{ exact: item.to === roleHome[role] }} className={topNavItemClassName}>
              {t(item.labelKey)}
            </Link>
          </li>
        ))}
      </ul>
      {overflow.length > 0 ? (
        <Suspense fallback={null}>
          <TopNavMore items={overflow} />
        </Suspense>
      ) : null}
    </nav>
  );
}
