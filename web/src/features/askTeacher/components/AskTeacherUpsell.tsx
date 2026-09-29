import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export function AskTeacherUpsell() {
  const { t } = useTranslation('askTeacher');

  return (
    <div className="flex flex-col items-start gap-3 rounded-lg border border-border bg-warning-soft p-4">
      <p className="text-ui text-text">{t('upsell.body')}</p>
      <Button asChild size="sm" variant="accent">
        <Link to="/student/subscription">{t('upsell.cta')}</Link>
      </Button>
    </div>
  );
}
