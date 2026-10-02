import { z } from 'zod';
import type { RuntimeSettingResult } from '@/shared/api/generated/model';
import { settingBound } from '../api/runtimeSettingValue';

const numericPattern = /^-?\d+(\.\d+)?$/;
const integerPattern = /^-?\d+$/;

const inRange = (setting: RuntimeSettingResult, value: number): boolean => {
  const minimum = settingBound(setting.minimum);
  const maximum = settingBound(setting.maximum);
  return (minimum === null || value >= minimum) && (maximum === null || value <= maximum);
};

export function numberSettingSchema(setting: RuntimeSettingResult) {
  return z.object({
    value: z
      .string()
      .trim()
      .min(1, 'validation.required')
      .refine((value) => numericPattern.test(value), 'validation.number')
      .refine((value) => setting.type !== 'Integer' || integerPattern.test(value), 'validation.integer')
      .refine((value) => inRange(setting, Number(value)), 'validation.range'),
  });
}

export const booleanSettingSchema = z.object({ value: z.boolean() });

export function choiceSettingSchema(setting: RuntimeSettingResult) {
  return z.object({
    value: z.string().refine((value) => setting.allowedValues.includes(value), 'validation.choice'),
  });
}

export function choiceListSettingSchema(setting: RuntimeSettingResult) {
  return z.object({
    value: z
      .array(z.string())
      .refine(
        (value) => value.every((item) => setting.allowedValues.includes(item)) && new Set(value).size === value.length,
        'validation.choice',
      ),
  });
}

export type NumberSettingValues = z.infer<ReturnType<typeof numberSettingSchema>>;
export type BooleanSettingValues = z.infer<typeof booleanSettingSchema>;
export type ChoiceSettingValues = z.infer<ReturnType<typeof choiceSettingSchema>>;
export type ChoiceListSettingValues = z.infer<ReturnType<typeof choiceListSettingSchema>>;
