import { useTranslation } from 'react-i18next';
import type { IntegrationMode, IntegrationProviderResult } from '@/shared/api/generated/model';
import { ConfigurationBadge, type ConfigurationBadgeTone } from './ConfigurationBadge';

export interface IntegrationTableProps {
  integrations: IntegrationProviderResult[];
}

const modeTones: Record<IntegrationMode, ConfigurationBadgeTone> = {
  Real: 'ok',
  Local: 'neutral',
  Fake: 'warning',
};

const cellClassName = 'px-2 py-2 text-start align-top';

export function IntegrationTable({ integrations }: IntegrationTableProps) {
  const { t } = useTranslation('configuration');

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-ui">
        <caption className="pb-2 text-start font-bold">{t('infrastructure.integrationsTitle')}</caption>
        <thead>
          <tr className="border-b border-border text-caption text-text-muted">
            <th scope="col" className={cellClassName}>
              {t('infrastructure.columns.integration')}
            </th>
            <th scope="col" className={cellClassName}>
              {t('infrastructure.columns.provider')}
            </th>
            <th scope="col" className={cellClassName}>
              {t('infrastructure.columns.mode')}
            </th>
          </tr>
        </thead>
        <tbody>
          {integrations.map((integration) => (
            <tr key={integration.integration} className="border-b border-border">
              <th scope="row" className={`${cellClassName} font-normal`}>
                {t(`integrations.${integration.integration}`)}
              </th>
              <td dir="ltr" className={cellClassName}>
                {integration.provider}
              </td>
              <td className={cellClassName}>
                <span className="flex flex-wrap gap-1">
                  <ConfigurationBadge tone={modeTones[integration.mode]}>
                    {t(`infrastructure.modes.${integration.mode}`)}
                  </ConfigurationBadge>
                  {integration.isEnabled ? null : (
                    <ConfigurationBadge tone="neutral">{t('infrastructure.disabled')}</ConfigurationBadge>
                  )}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
