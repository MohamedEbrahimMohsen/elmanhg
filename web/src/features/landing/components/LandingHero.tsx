import { useId } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { marketedQuestionGoal } from '../api/marketing';

export interface LandingHeroProps {
  servableCount: number | undefined;
}

export function LandingHero({ servableCount }: LandingHeroProps) {
  const { t } = useTranslation('landing');
  const headingId = useId();

  return (
    <section
      aria-labelledby={headingId}
      className="flex flex-col items-start gap-3 rounded-lg bg-aurora p-5 text-surface shadow-1 lg:p-8"
    >
      <p className="text-caption font-semibold">{t('hero.eyebrow')}</p>
      <h1 id={headingId} className="font-display text-display font-bold lg:text-display-desktop">
        {servableCount === undefined ? t('hero.title') : t('hero.titleWithCount', { count: servableCount })}
      </h1>
      <p className="text-ui text-surface/80">{t('hero.goal', { goal: marketedQuestionGoal })}</p>
      <Button asChild variant="primary">
        <Link to="/signup">{t('hero.start')}</Link>
      </Button>
    </section>
  );
}
