import { useTranslation } from 'react-i18next';
import type { SecretStatusResult } from '@/shared/api/generated/model';
import { ConfigurationBadge } from './ConfigurationBadge';

export interface SecretStatusListProps {
  secrets: SecretStatusResult[];
}

export function SecretStatusList({ secrets }: SecretStatusListProps) {
  const { t } = useTranslation('configuration');

  return (
    <ul className="flex flex-col gap-2">
      {secrets.map((secret) => (
        <li key={secret.key} className="flex flex-wrap items-center justify-between gap-2">
          <span dir="ltr" className="font-mono text-caption text-text">
            {secret.key}
          </span>
          <ConfigurationBadge tone={secret.isSet ? 'ok' : 'neutral'}>
            {t(secret.isSet ? 'secrets.set' : 'secrets.notSet')}
          </ConfigurationBadge>
        </li>
      ))}
    </ul>
  );
}
