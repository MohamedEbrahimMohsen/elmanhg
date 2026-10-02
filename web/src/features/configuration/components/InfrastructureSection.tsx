import { useTranslation } from 'react-i18next';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { Button } from '@/shared/ui/button';
import { useGetInfrastructureConfiguration } from '../hooks/useInfrastructureConfiguration';
import { AiServiceDetails } from './AiServiceDetails';
import { ConfigurationBadge } from './ConfigurationBadge';
import { ConfigurationSkeleton } from './ConfigurationSkeleton';
import { IntegrationTable } from './IntegrationTable';
import { SecretStatusList } from './SecretStatusList';

const allowFakePaymentsKey = 'Payments:AllowFakePayments';
const subtitleClassName = 'font-display text-h3 font-bold';

export function InfrastructureSection() {
  const { t } = useTranslation('configuration');
  const { data, error, isPending, isError, refetch } = useGetInfrastructureConfiguration();

  const renderBody = () => {
    if (isPending) {
      return <ConfigurationSkeleton label={t('infrastructure.loading')} />;
    }
    if (isError) {
      const code = error instanceof ApiError ? error.code : unhandledErrorCode;
      return (
        <div
          role="alert"
          className="flex flex-col items-start gap-3 rounded-lg border border-danger bg-danger-soft p-4"
        >
          <p className="text-ui font-semibold text-danger">{t('infrastructure.errorTitle')}</p>
          <p className="text-caption text-text">{t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>
          <Button variant="secondary" onClick={() => void refetch()}>
            {t('common:actions.retry')}
          </Button>
        </div>
      );
    }
    return (
      <>
        <dl className="flex gap-2">
          <dt className="text-caption text-text-muted">{t('infrastructure.environment')}</dt>
          <dd dir="ltr" className="text-ui font-semibold">
            {data.environment}
          </dd>
        </dl>
        <IntegrationTable integrations={data.integrations} />
        <h3 className={subtitleClassName}>{t('safety.title')}</h3>
        <ul className="flex flex-col gap-2">
          {data.safetySwitches.map((safetySwitch) => (
            <li key={safetySwitch.key} className="flex flex-wrap items-center gap-2">
              <span className="text-ui">
                {safetySwitch.key === allowFakePaymentsKey ? t('safety.allowFakePayments') : safetySwitch.key}
              </span>
              <span dir="ltr" className="font-mono text-caption text-text-muted">
                {safetySwitch.key}
              </span>
              <ConfigurationBadge tone={safetySwitch.isOn ? 'warning' : 'neutral'}>
                {t(safetySwitch.isOn ? 'row.on' : 'row.off')}
              </ConfigurationBadge>
            </li>
          ))}
        </ul>
        <p className="text-caption text-text-muted">{t('safety.note')}</p>
        <h3 className={subtitleClassName}>{t('secrets.title')}</h3>
        <SecretStatusList secrets={data.secrets} />
        <h3 className={subtitleClassName}>{t('ai.title')}</h3>
        <AiServiceDetails status={data.aiServiceStatus} aiService={data.aiService} />
      </>
    );
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('infrastructure.title')}</h2>
      <p className="text-caption text-text-muted">{t('infrastructure.intro')}</p>
      {renderBody()}
    </div>
  );
}
