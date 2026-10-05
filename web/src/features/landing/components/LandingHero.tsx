import { useId } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';
import { heroClassName, heroHaloClassName } from '@/shared/ui/hero';
import { marketedQuestionGoal } from '../api/marketing';

export interface LandingHeroProps {
  servableCount: number | undefined;
}

export function LandingHero({ servableCount }: LandingHeroProps) {
  const { t } = useTranslation('landing');
  const headingId = useId();

  return (
    <section aria-labelledby={headingId} className={cn(heroClassName, 'items-start')}>
      <span aria-hidden="true" className={heroHaloClassName} />
      <p className="text-caption font-bold">{t('hero.eyebrow')}</p>
      <h1 id={headingId} className="max-w-180 font-display text-display font-extrabold lg:text-display-desktop">
        {servableCount === undefined ? t('hero.title') : t('hero.titleWithCount', { count: servableCount })}
      </h1>
      <p className="max-w-120 text-ui text-surface">{t('hero.goal', { goal: marketedQuestionGoal })}</p>
      <Button asChild>
        <Link to="/signup">{t('hero.start')}</Link>
      </Button>
    </section>
  );
}
