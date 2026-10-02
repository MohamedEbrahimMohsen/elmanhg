import { useTranslation } from 'react-i18next';
import type { RuntimeSettingGroupResult } from '@/shared/api/generated/model';
import { RuntimeSettingRow } from './RuntimeSettingRow';

export interface RuntimeSettingGroupCardProps {
  group: RuntimeSettingGroupResult;
}

export function RuntimeSettingGroupCard({ group }: RuntimeSettingGroupCardProps) {
  const { t } = useTranslation('configuration');

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t(`groups.${group.group}.title`)}</h2>
      <p className="text-caption text-text-muted">{t(`groups.${group.group}.description`)}</p>
      <ul className="flex flex-col divide-y divide-border">
        {group.settings.map((setting) => (
          <RuntimeSettingRow key={setting.key} setting={setting} />
        ))}
      </ul>
    </div>
  );
}
