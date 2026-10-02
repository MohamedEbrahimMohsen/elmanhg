import { describe, expect, it } from 'vitest';
import { examPeriodSchema } from './examPeriodSchema';

const valid = { name: 'Final exams', startDate: '2026-06-01', endDate: '2026-07-15' };

function firstIssue(values: Record<string, string>) {
  const issue = examPeriodSchema.safeParse(values).error?.issues[0];
  return { path: issue?.path, message: issue?.message };
}

describe('examPeriodSchema', () => {
  it('accepts a valid period', () => {
    expect(examPeriodSchema.parse({ ...valid, name: '  Final exams ' })).toEqual(valid);
  });

  it('requires a name', () => {
    expect(firstIssue({ ...valid, name: '   ' })).toEqual({
      path: ['name'],
      message: 'configuration:examPeriods.validation.nameRequired',
    });
  });

  it('requires the first day', () => {
    expect(firstIssue({ ...valid, startDate: '' })).toEqual({
      path: ['startDate'],
      message: 'configuration:examPeriods.validation.startRequired',
    });
  });

  it('requires the last day', () => {
    expect(firstIssue({ ...valid, endDate: '' })).toEqual({
      path: ['endDate'],
      message: 'configuration:examPeriods.validation.endRequired',
    });
  });

  it('rejects a last day before the first day', () => {
    expect(firstIssue({ ...valid, endDate: '2026-05-31' })).toEqual({
      path: ['endDate'],
      message: 'configuration:examPeriods.validation.rangeInvalid',
    });
  });

  it('accepts a one-day period', () => {
    expect(examPeriodSchema.safeParse({ ...valid, endDate: valid.startDate }).success).toBe(true);
  });
});
