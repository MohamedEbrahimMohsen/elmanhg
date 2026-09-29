import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export function LandingTopBar() {
  const { t } = useTranslation('landing');

  return (
    <header className="mx-auto flex max-w-layout items-center gap-3 px-4 py-3 lg:px-6">
      <p className="font-display text-h3 font-bold">{t('common:app.name')}</p>
      <Button asChild variant="secondary" size="sm" className="ms-auto">
        <Link to="/login">{t('topBar.signIn')}</Link>
      </Button>
    </header>
  );
}
