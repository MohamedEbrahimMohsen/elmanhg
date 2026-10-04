import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import { Button } from '@/shared/ui/button';

export interface WeakSpotRowProps {
  title: string;
  meta: string;
  percent: number;
  barLabel: string;
  lessonId: string;
}

export function WeakSpotRow({ title, meta, percent, barLabel, lessonId }: WeakSpotRowProps) {
  const { t } = useTranslation('progress');

  return (
    <li className="flex flex-col gap-2 py-3 md:flex-row md:items-center md:gap-4">
      <div className="flex min-w-0 flex-1 flex-col">
        <p className="text-ui font-semibold text-text">{title}</p>
        <p className="text-caption text-text-muted">{meta}</p>
      </div>
      <div className="flex items-center gap-3">
        <div className="flex flex-1 flex-col gap-1 md:w-28 md:flex-none">
          <p className="text-caption text-text-muted">{t('weakSpots.mastery', { percent })}</p>
          <MasteryBar percent={percent} label={barLabel} />
        </div>
        <Button asChild size="sm" variant="secondary">
          <Link to="/student/lesson/$lessonId/practice" params={{ lessonId }}>
            {t('weakSpots.train')}
          </Link>
        </Button>
      </div>
    </li>
  );
}
