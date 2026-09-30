import { Star } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';

export interface ThreadRatingProps {
  rating: number;
}

const positions = [1, 2, 3, 4, 5] as const;

export function ThreadRating({ rating }: ThreadRatingProps) {
  const { t } = useTranslation('askTeacher');

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1">
      <p className="text-caption text-text-muted">{t('rating.label')}</p>
      <span role="img" aria-label={t('rating.value', { rating })} className="flex gap-1">
        {positions.map((position) => (
          <Star
            key={position}
            aria-hidden
            className={cn('size-5', position <= rating ? 'fill-current text-text' : 'text-text-muted')}
          />
        ))}
      </span>
    </div>
  );
}
