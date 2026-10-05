import { useTranslation } from 'react-i18next';
import { BrandBar } from '@/shared/components/BrandBar';
import { privacyContactEmail, termsVersion } from '@/shared/lib/terms';
import { cn } from '@/shared/lib/utils';
import { layoutContainerClassName } from '@/shared/ui/layout';
import { PrivacySection } from '../components/PrivacySection';
import { registerLegalLocales } from '../locales';

registerLegalLocales();

const sectionKeys = ['collect', 'use', 'training', 'deleteChats', 'minors'] as const;

export function PrivacyPage() {
  const { t } = useTranslation('legal');

  return (
    <div className="flex min-h-dvh flex-col">
      <BrandBar />
      <main id="main" className={cn(layoutContainerClassName, 'flex flex-col gap-6 pt-6 pb-10')}>
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('privacy.title')}</h1>
          <p className="text-caption text-text-muted">{t('privacy.version', { version: termsVersion })}</p>
        </div>
        <p className="text-body">{t('privacy.intro')}</p>
        <div className="flex flex-col gap-5 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
          {sectionKeys.map((key) => (
            <PrivacySection key={key} title={t(`privacy.${key}.title`)}>
              <p className="text-body">{t(`privacy.${key}.body`)}</p>
            </PrivacySection>
          ))}
          <PrivacySection title={t('privacy.contact.title')}>
            <p className="text-body">
              {t('privacy.contact.body')}{' '}
              <a
                href={`mailto:${privacyContactEmail}`}
                dir="ltr"
                className="font-bold text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
              >
                {privacyContactEmail}
              </a>
            </p>
          </PrivacySection>
        </div>
      </main>
    </div>
  );
}
