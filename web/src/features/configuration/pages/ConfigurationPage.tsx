import { useTranslation } from 'react-i18next';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { ConfigurationSkeleton } from '../components/ConfigurationSkeleton';
import { InfrastructureSection } from '../components/InfrastructureSection';
import { RuntimeSettingGroupCard } from '../components/RuntimeSettingGroupCard';
import { useGetRuntimeSettings } from '../hooks/useRuntimeSettings';
import { registerConfigurationLocales } from '../locales';

registerConfigurationLocales();

export function ConfigurationPage() {
  const { t } = useTranslation('configuration');
  const { data, error, isPending, isError, refetch } = useGetRuntimeSettings();

  const renderSettings = () => {
    if (isPending) {
      return <ConfigurationSkeleton label={t('settings.loading')} />;
    }
    if (isError) {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-semibold text-danger">{t('settings.errorTitle')}</p>
          <p className="text-caption text-text">{t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>
          <Button variant="secondary" onClick={() => void refetch()}>
            {t('common:actions.retry')}
          </Button>
        </div>
      );
    }
    if (data.length === 0) {
      return <p className="text-ui text-text-muted">{t('settings.empty')}</p>;
    }
    return data.map((group) => <RuntimeSettingGroupCard key={group.group} group={group} />);
  };

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>
      <p className="text-ui text-text">{t('page.intro')}</p>
      {renderSettings()}
      <InfrastructureSection />
    </section>
  );
}
