import { useTranslation } from 'react-i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { formatDateTime } from '@/shared/lib/dateTime';
import { Button } from '@/shared/ui/button';
import { formatSettingValue, readSettingValue, settingDescription, settingLabel } from '../api/runtimeSettingValue';
import { useRuntimeSettingMutations } from '../hooks/useRuntimeSettingMutations';
import { BooleanSettingForm } from './BooleanSettingForm';
import { ChoiceListSettingForm } from './ChoiceListSettingForm';
import { ChoiceSettingForm } from './ChoiceSettingForm';
import { ConfigurationBadge } from './ConfigurationBadge';
import { NumberSettingForm } from './NumberSettingForm';

export interface RuntimeSettingRowProps {
  setting: RuntimeSettingResult;
}

export function RuntimeSettingRow({ setting }: RuntimeSettingRowProps) {
  const { t, i18n } = useTranslation('configuration');
  const lng = i18n.language;
  const mutations = useRuntimeSettingMutations(setting.key);
  const value = formatSettingValue(setting, readSettingValue(setting, setting.value), lng, t);
  const formKey = JSON.stringify(setting.value);
  const defaultValue = formatSettingValue(setting, readSettingValue(setting, setting.defaultValue), lng, t);

  const renderForm = () => {
    switch (setting.type) {
      case 'Integer':
      case 'Decimal':
        return <NumberSettingForm key={formKey} setting={setting} mutations={mutations} />;
      case 'Boolean':
        return <BooleanSettingForm key={formKey} setting={setting} mutations={mutations} />;
      case 'Choice':
        return <ChoiceSettingForm key={formKey} setting={setting} mutations={mutations} />;
      case 'ChoiceList':
        return <ChoiceListSettingForm key={formKey} setting={setting} mutations={mutations} />;
    }
  };

  return (
    <li className="flex flex-col gap-2 py-3">
      <p className="text-ui font-bold">{settingLabel(setting, lng)}</p>
      <p className="text-caption text-text-muted">{settingDescription(setting, lng)}</p>
      <div className="flex flex-wrap items-center gap-2">
        <span dir="ltr" className="text-ui font-bold">
          {value}
        </span>
        <ConfigurationBadge tone={setting.isOverridden ? 'accent' : 'neutral'}>
          {t(setting.isOverridden ? 'row.overridden' : 'row.usingDefault')}
        </ConfigurationBadge>
      </div>
      <p className="text-caption text-text-muted">{t('row.default', { value: defaultValue })}</p>
      {setting.updatedAt ? (
        <p className="text-caption text-text-muted">
          {t('row.updatedAt', { date: formatDateTime(setting.updatedAt, lng, 'date') })}
        </p>
      ) : null}
      {renderForm()}
      {setting.isOverridden ? (
        <div>
          <Button
            variant="secondary"
            size="sm"
            disabled={mutations.isPending}
            onClick={() => {
              void mutations.reset().catch(() => undefined);
            }}
          >
            {t('row.reset')}
          </Button>
        </div>
      ) : null}
    </li>
  );
}
