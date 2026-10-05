import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export function TermsNotice() {
  const { t } = useTranslation('session');

  return (
    <p className="text-caption text-text-muted">
      {t('signUp.terms.notice')}{' '}
      <Link
        to="/privacy"
        target="_blank"
        rel="noopener noreferrer"
        className="font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
      >
        {t('signUp.terms.link')}
        <span className="sr-only"> {t('signUp.terms.newTab')}</span>
      </Link>
    </p>
  );
}
