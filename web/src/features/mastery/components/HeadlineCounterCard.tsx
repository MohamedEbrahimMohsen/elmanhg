import { useTranslation } from 'react-i18next';
import type { MasteryHeadlineResult } from '@/shared/api/generated/model';

export interface HeadlineCounterCardProps {
  headline: MasteryHeadlineResult;
  streakDays: number;
}

export function HeadlineCounterCard({ headline, streakDays }: HeadlineCounterCardProps) {
  const { t } = useTranslation('mastery');

  return (
    <section
      aria-label={t('headline.label')}
      className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <p className="font-display text-display font-bold lg:text-display-desktop">
        {t('headline.remaining', { remaining: Number(headline.remainingCount), total: Number(headline.servableTotal) })}
      </p>
      <p className="text-caption text-text-muted">
        {t('headline.meta', {
          seen: Number(headline.seenCount),
          mastered: Number(headline.masteredCount),
          streak: streakDays,
        })}
      </p>
    </section>
  );
}
