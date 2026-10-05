import { Link } from '@tanstack/react-router';
import { Ellipsis } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { roleHome, type Role } from '@/features/session';
import { navByRole, navIconStrokeWidth, tabBarItems, type NavItem } from '../navConfig';

export interface TabBarProps {
  role: Role;
}

export function TabBar({ role }: TabBarProps) {
  const { t } = useTranslation('shell');
  const nav = navByRole[role];
  const items: readonly Omit<NavItem, 'capability'>[] = [
    ...tabBarItems(role, nav),
    ...(nav.morePath ? [{ key: 'more', to: nav.morePath, labelKey: 'nav.more', icon: Ellipsis }] : []),
  ];

  return (
    <nav
      aria-label={t('nav.tabBar')}
      className="fixed inset-x-0 bottom-0 z-20 border-t border-border bg-surface pb-safe-area lg:hidden"
    >
      <ul className="flex">
        {items.map(({ key, to, labelKey, icon: Icon }) => (
          <li key={key} className="flex-1">
            <Link
              to={to}
              activeOptions={{ exact: to === roleHome[role] }}
              className="group flex min-h-14 flex-col items-center justify-center gap-0.5 text-micro text-text-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden data-[status=active]:font-bold data-[status=active]:text-accent-text"
            >
              <span
                aria-hidden
                className="inline-flex h-7 w-14 items-center justify-center rounded-pill group-data-[status=active]:bg-accent-soft"
              >
                <Icon aria-hidden className="size-5.5" strokeWidth={navIconStrokeWidth} />
              </span>
              <span className="leading-none">{t(labelKey)}</span>
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
