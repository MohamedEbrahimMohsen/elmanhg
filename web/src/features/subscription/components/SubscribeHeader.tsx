import { useTranslation } from 'react-i18next';
import { useGetServableQuestionCount } from '@/shared/api/generated/questions/questions';
import { heroClassName, heroHaloClassName } from '@/shared/ui/hero';

export function SubscribeHeader() {
  const { t } = useTranslation('subscription');
  const { data } = useGetServableQuestionCount();

  return (
    <header className={heroClassName}>
      <span aria-hidden="true" className={heroHaloClassName} />
      <h1 className="font-display text-display font-extrabold lg:text-display-desktop">{t('header.title')}</h1>
      <p className="max-w-120 text-ui text-surface">
        {data ? t('header.taglineWithCount', { count: Number(data.count) }) : t('header.tagline')}
      </p>
    </header>
  );
}
