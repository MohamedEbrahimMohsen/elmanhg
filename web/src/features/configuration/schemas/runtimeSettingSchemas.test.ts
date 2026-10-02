import { describe, expect, it } from 'vitest';
import { choiceListSetting, choiceSetting, runtimeSetting } from '@/test/configurationFixtures';
import { choiceListSettingSchema, choiceSettingSchema, numberSettingSchema } from './runtimeSettingSchemas';

const firstIssue = (result: { success: boolean; error?: { issues: { message: string }[] } }) =>
  result.error?.issues[0]?.message;

describe('numberSettingSchema', () => {
  const integer = numberSettingSchema(runtimeSetting());
  const decimal = numberSettingSchema(
    runtimeSetting({ key: 'grading.essayReviewConfidenceThreshold', type: 'Decimal', minimum: 0, maximum: 1 }),
  );

  it('accepts an integer inside the range', () => {
    expect(integer.safeParse({ value: '30' })).toEqual({ success: true, data: { value: '30' } });
  });

  it('rejects below min with validation.range', () => {
    expect(firstIssue(integer.safeParse({ value: '0' }))).toBe('validation.range');
  });

  it('rejects a fraction for an Integer with validation.integer', () => {
    expect(firstIssue(integer.safeParse({ value: '12.5' }))).toBe('validation.integer');
  });

  it('rejects an empty value with validation.required', () => {
    expect(firstIssue(integer.safeParse({ value: '  ' }))).toBe('validation.required');
  });

  it('accepts a decimal for a Decimal setting', () => {
    expect(decimal.safeParse({ value: '0.75' }).success).toBe(true);
  });

  it('rejects a decimal above max', () => {
    expect(firstIssue(decimal.safeParse({ value: '1.2' }))).toBe('validation.range');
  });
});

describe('choice schemas', () => {
  it('rejects a choice outside the allowed values', () => {
    expect(firstIssue(choiceSettingSchema(choiceSetting()).safeParse({ value: 'Sms' }))).toBe('validation.choice');
  });

  it('accepts a subset for a choice list', () => {
    expect(choiceListSettingSchema(choiceListSetting()).safeParse({ value: ['Email'] }).success).toBe(true);
  });

  it('rejects an unknown item in a choice list', () => {
    expect(firstIssue(choiceListSettingSchema(choiceListSetting()).safeParse({ value: ['Email', 'Sms'] }))).toBe(
      'validation.choice',
    );
  });
});
