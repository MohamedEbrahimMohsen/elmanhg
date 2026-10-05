import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

export function BlueprintsEmptyState() {
  const { t } = useTranslation('blueprints');

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-border bg-surface p-6 text-center shadow-1">
      <p className="text-ui text-text">{t('page.noSubjects')}</p>
      <Link to="/admin/content" className="text-ui font-bold text-accent-text underline-offset-4 hover:underline">
        {t('page.goToContent')}
      </Link>
    </div>
  );
}
