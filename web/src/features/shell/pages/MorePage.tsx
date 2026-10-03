import { Link } from '@tanstack/react-router';
import { ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { Role } from '@/features/session';
import { navByRole, navIconStrokeWidth, overflowItems } from '../navConfig';

export interface MorePageProps {
  role: Role;
}

export function MorePage({ role }: MorePageProps) {
  const { t } = useTranslation('shell');

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('more.title')}</h1>
      <ul className="flex flex-col gap-2">
        {overflowItems(role, navByRole[role]).map(({ key, to, labelKey, icon: Icon }) => (
          <li key={key}>
            <Link
              to={to}
              className="flex min-h-11 items-center gap-3 rounded-md border border-border bg-surface px-3.5 py-3 text-ui font-semibold text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden"
            >
              <Icon aria-hidden className="size-5.5" strokeWidth={navIconStrokeWidth} />
              <span className="flex-1">{t(labelKey)}</span>
              <ChevronRight aria-hidden className="size-4 rtl:rotate-180" />
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
