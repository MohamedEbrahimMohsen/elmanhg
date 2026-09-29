import { useTranslation } from 'react-i18next';
import { useGetServableQuestionCount } from '@/shared/api/generated/questions/questions';

export function SubscribeHeader() {
  const { t } = useTranslation('subscription');
  const { data } = useGetServableQuestionCount();

  return (
    <header className="flex flex-col gap-2 rounded-lg bg-aurora p-5 text-surface shadow-1">
      <h1 className="font-display text-display font-bold lg:text-display-desktop">{t('header.title')}</h1>
      <p className="text-ui text-surface/80">
        {data ? t('header.taglineWithCount', { count: Number(data.count) }) : t('header.tagline')}
      </p>
    </header>
  );
}
