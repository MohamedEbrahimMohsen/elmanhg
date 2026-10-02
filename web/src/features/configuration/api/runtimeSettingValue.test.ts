import { beforeAll, describe, expect, it } from 'vitest';
import { i18n } from '@/app/i18n';
import { choiceListSetting, featureFlag, runtimeSetting } from '@/test/configurationFixtures';
import { registerConfigurationLocales } from '../locales';
import { formatSettingValue, readSettingValue, settingLabel } from './runtimeSettingValue';

describe('runtimeSettingValue', () => {
  beforeAll(() => {
    registerConfigurationLocales();
  });

  const format = (setting = runtimeSetting(), value: number | boolean | string[] = 1200, lng = 'en') =>
    formatSettingValue(setting, value, lng, i18n.getFixedT(lng));

  it('formats an integer with ASCII digits in en and ar', () => {
    expect(format(runtimeSetting(), 1200, 'en')).toBe('1,200');
    expect(format(runtimeSetting(), 1200, 'ar')).toMatch(/^1.?200$/);
  });

  it('formats a boolean as On and Off in en and مفعّل and متوقف in ar', () => {
    expect([format(featureFlag(), true, 'en'), format(featureFlag(), false, 'en')]).toEqual(['On', 'Off']);
    expect([format(featureFlag(), true, 'ar'), format(featureFlag(), false, 'ar')]).toEqual(['مفعّل', 'متوقف']);
  });

  it('joins a choice list', () => {
    expect(format(choiceListSetting(), ['Email', 'WhatsApp'], 'en')).toBe('WhatsApp, Email');
    expect(format(choiceListSetting(), ['Email', 'WhatsApp'], 'ar')).toBe('WhatsApp، Email');
  });

  it('shows None for an empty list', () => {
    expect(format(choiceListSetting(), [], 'en')).toBe('None');
  });

  it('throws when the raw value does not match the type', () => {
    expect(() => readSettingValue(runtimeSetting(), 'twenty')).toThrow('Unexpected value for askTeacher.replySlaHours');
    expect(readSettingValue(featureFlag(), true)).toBe(true);
  });

  it('picks the Arabic label for ar', () => {
    expect(settingLabel(runtimeSetting(), 'ar')).toBe('مهلة رد المعلّم (ساعات)');
    expect(settingLabel(runtimeSetting(), 'en')).toBe('Teacher reply time (hours)');
  });
});
