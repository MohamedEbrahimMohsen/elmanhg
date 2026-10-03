import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { BrandBar } from '@/shared/components/BrandBar';
import { Button } from '@/shared/ui/button';

export function LandingTopBar() {
  const { t } = useTranslation('landing');

  return (
    <BrandBar>
      <Button asChild variant="secondary" size="sm">
        <Link to="/login">{t('topBar.signIn')}</Link>
      </Button>
    </BrandBar>
  );
}
