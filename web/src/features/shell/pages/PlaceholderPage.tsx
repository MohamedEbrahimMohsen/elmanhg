import { useTranslation } from 'react-i18next';

export interface PlaceholderPageProps {
  titleKey: string;
}

export function PlaceholderPage({ titleKey }: PlaceholderPageProps) {
  const { t } = useTranslation('shell');

  return (
    <section className="flex flex-col gap-3">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t(titleKey)}</h1>
      <p className="text-caption text-text-muted">{t('placeholder.body')}</p>
    </section>
  );
}
