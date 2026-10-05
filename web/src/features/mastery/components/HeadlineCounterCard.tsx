import { useTranslation } from 'react-i18next';
import type { MasteryHeadlineResult } from '@/shared/api/generated/model';
import { heroClassName, heroHaloClassName } from '@/shared/ui/hero';
import { MasteryBar } from './MasteryBar';

export interface HeadlineCounterCardProps {
  headline: MasteryHeadlineResult;
  streakDays: number;
}

export function HeadlineCounterCard({ headline, streakDays }: HeadlineCounterCardProps) {
  const { t } = useTranslation('mastery');
  const total = Number(headline.servableTotal);
  const mastered = Number(headline.masteredCount);
  const masteredPercent = total > 0 ? Math.round((mastered * 100) / total) : 0;
  const stats = [
    { key: 'seen', label: t('headline.seen'), value: t('headline.count', { value: Number(headline.seenCount) }) },
    { key: 'mastered', label: t('headline.mastered'), value: t('headline.count', { value: mastered }) },
    { key: 'streak', label: t('headline.streak'), value: t('headline.streakValue', { streak: streakDays }) },
  ];

  return (
    <section aria-label={t('headline.label')} className={heroClassName}>
      <span aria-hidden="true" className={heroHaloClassName} />
      <p className="font-display text-h1 font-extrabold text-balance lg:text-display-desktop">
        {t('headline.remaining', { remaining: Number(headline.remainingCount), total })}
      </p>
      <MasteryBar tone="onHero" percent={masteredPercent} label={t('headline.barLabel')} />
      <dl className="flex flex-wrap gap-2">
        {stats.map((stat) => (
          <div key={stat.key} className="flex items-baseline gap-1.5 rounded-pill bg-surface px-3 py-1">
            <dt className="text-caption text-text-muted">{stat.label}</dt>
            <dd className="text-caption font-bold text-text">{stat.value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
