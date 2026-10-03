import type { TFunction } from 'i18next';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';

export type SettingValue = number | boolean | string | string[];

const isStringList = (raw: unknown): raw is string[] =>
  Array.isArray(raw) && raw.every((item) => typeof item === 'string');

export function readSettingValue(setting: RuntimeSettingResult, raw: unknown): SettingValue {
  switch (setting.type) {
    case 'Integer':
    case 'Decimal':
      if (typeof raw === 'number') {
        return raw;
      }
      break;
    case 'Boolean':
      if (typeof raw === 'boolean') {
        return raw;
      }
      break;
    case 'Choice':
      if (typeof raw === 'string') {
        return raw;
      }
      break;
    case 'ChoiceList':
      if (isStringList(raw)) {
        return raw;
      }
      break;
  }
  throw new Error('Unexpected value for ' + setting.key);
}

export function settingBound(bound: number | string | null): number | null {
  return bound === null ? null : Number(bound);
}

export function formatSettingNumber(value: number, lng: string): string {
  return formatNumber(value, lng, { maximumFractionDigits: 4 });
}

export function formatSettingValue(
  setting: RuntimeSettingResult,
  value: SettingValue,
  lng: string,
  t: TFunction,
): string {
  if (typeof value === 'number') {
    return formatSettingNumber(value, lng);
  }
  if (typeof value === 'boolean') {
    return t(value ? 'configuration:row.on' : 'configuration:row.off');
  }
  if (typeof value === 'string') {
    return t([`configuration:choices.${value}`, value]);
  }
  if (value.length === 0) {
    return t('configuration:row.none');
  }
  return setting.allowedValues
    .filter((item) => value.includes(item))
    .map((item) => t([`configuration:choices.${item}`, item]))
    .join(lng.startsWith('ar') ? '، ' : ', ');
}

export function settingLabel(setting: RuntimeSettingResult, lng: string): string {
  return lng.startsWith('ar') ? setting.labelArabic : setting.labelEnglish;
}

export function settingDescription(setting: RuntimeSettingResult, lng: string): string {
  return lng.startsWith('ar') ? setting.descriptionArabic : setting.descriptionEnglish;
}
