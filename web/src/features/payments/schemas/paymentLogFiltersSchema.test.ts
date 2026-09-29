import { describe, expect, it } from 'vitest';
import { paymentLogFiltersSchema } from './paymentLogFiltersSchema';
import { paymentLogReferenceMaxLength } from './paymentLogSearchSchema';

const blank = { status: '', plan: '', reference: '', from: '', to: '' };

describe('paymentLogFiltersSchema', () => {
  it('accepts valid filters', () => {
    expect(
      paymentLogFiltersSchema.safeParse({
        status: 'Succeeded',
        plan: 'Base',
        reference: '192036465',
        from: '2026-09-01',
        to: '2026-09-01',
      }).success,
    ).toBe(true);
    expect(paymentLogFiltersSchema.safeParse(blank).success).toBe(true);
  });

  it('rejects a reference over 100 characters', () => {
    const result = paymentLogFiltersSchema.safeParse({
      ...blank,
      reference: '1'.repeat(paymentLogReferenceMaxLength + 1),
    });

    expect(result.error?.issues[0]?.message).toBe('payments:filters.errors.referenceTooLong');
  });

  it('rejects an end date before the start date', () => {
    const result = paymentLogFiltersSchema.safeParse({ ...blank, from: '2026-09-10', to: '2026-09-01' });

    expect(result.error?.issues[0]?.message).toBe('payments:filters.errors.dateRange');
    expect(result.error?.issues[0]?.path).toEqual(['to']);
  });
});
