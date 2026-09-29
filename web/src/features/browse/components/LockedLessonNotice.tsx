import { Link } from '@tanstack/react-router';
import { Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export function LockedLessonNotice() {
  const { t } = useTranslation('browse');

  return (
    <div className="flex flex-col items-start gap-2 rounded-lg border border-warning bg-warning-soft p-4">
      <p className="flex items-start gap-2 text-ui text-text">
        <Lock aria-hidden className="size-4 shrink-0" />
        {t('lesson.lockedNotice')}
      </p>
      <Button asChild size="sm" variant="primary">
        <Link to="/student/subscription">{t('lesson.subscribe')}</Link>
      </Button>
    </div>
  );
}
