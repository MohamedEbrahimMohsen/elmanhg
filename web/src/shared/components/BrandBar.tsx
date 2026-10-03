import type { ReactNode } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { appBarClassName, appBarRowClassName, logoClassName } from '@/shared/ui/layout';

export interface BrandBarProps {
  children?: ReactNode;
}

export function BrandBar({ children }: BrandBarProps) {
  const { t } = useTranslation();

  return (
    <header className={appBarClassName}>
      <div className={appBarRowClassName}>
        <Link to="/" className={logoClassName}>
          {t('common:app.name')}
        </Link>
        {children ? <div className="ms-auto flex items-center gap-2">{children}</div> : null}
      </div>
    </header>
  );
}
