import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';

export interface LessonStateBadgeProps {
  state: string;
}

export function LessonStateBadge({ state }: LessonStateBadgeProps) {
  const { t } = useTranslation('content');

  return (
    <span
      className={cn(
        'rounded-full px-2.5 py-0.5 text-micro font-semibold',
        state === 'Published' ? 'bg-success-soft text-success-text' : 'bg-soft text-text-muted',
      )}
    >
      {t([`lessons.state.${state}`, 'lessons.state.Draft'])}
    </span>
  );
}
