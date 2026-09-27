import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { roleHome, type Role } from '@/features/session';
import { navByRole, visibleNavItems } from '../navConfig';

export interface TopTabsProps {
  role: Role;
}

export function TopTabs({ role }: TopTabsProps) {
  const { t } = useTranslation('shell');

  return (
    <nav aria-label={t('nav.main')} className="hidden lg:block">
      <ul className="mx-auto flex max-w-layout gap-1 overflow-x-auto px-4 lg:px-6">
        {visibleNavItems(role, navByRole[role]).map((item) => (
          <li key={item.key}>
            <Link
              to={item.to}
              activeOptions={{ exact: item.to === roleHome[role] }}
              className="inline-flex min-h-11 items-center border-b-2 border-transparent px-3.5 text-ui font-medium text-text-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden data-[status=active]:border-accent data-[status=active]:font-semibold data-[status=active]:text-text"
            >
              {t(item.labelKey)}
            </Link>
          </li>
        ))}
      </ul>
    </nav>
  );
}
