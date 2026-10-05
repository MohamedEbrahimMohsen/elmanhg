import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { layoutContainerClassName } from '@/shared/ui/layout';

export function LandingFooter() {
  const { t } = useTranslation('landing');

  return (
    <footer className={cn(layoutContainerClassName, 'border-t border-border py-6')}>
      <Link
        to="/privacy"
        className="text-caption font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        {t('footer.privacy')}
      </Link>
    </footer>
  );
}
