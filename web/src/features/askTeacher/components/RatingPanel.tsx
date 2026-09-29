import { useId } from 'react';
import { Star } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { useRateThread } from '../hooks/useRateThread';

export interface RatingPanelProps {
  threadId: string;
  closes: boolean;
}

const ratings = [1, 2, 3, 4, 5] as const;

export function RatingPanel({ threadId, closes }: RatingPanelProps) {
  const { t } = useTranslation('askTeacher');
  const { rate, isPending } = useRateThread(threadId);
  const headingId = useId();

  return (
    <div className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 id={headingId} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {t(closes ? 'rating.titleClose' : 'rating.title')}
      </h2>
      <div role="group" aria-labelledby={headingId} className="flex flex-wrap gap-2">
        {ratings.map((rating) => (
          <Button
            key={rating}
            variant="secondary"
            aria-label={t('rating.option', { rating })}
            disabled={isPending}
            onClick={() => {
              rate(rating);
            }}
          >
            <span>{rating}</span>
            <Star className="size-4" aria-hidden />
          </Button>
        ))}
      </div>
    </div>
  );
}
