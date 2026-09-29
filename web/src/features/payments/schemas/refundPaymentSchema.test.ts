import { describe, expect, it } from 'vitest';
import { refundPaymentSchema, refundReasonMaxLength } from './refundPaymentSchema';

describe('refundPaymentSchema', () => {
  it('accepts a reason', () => {
    expect(refundPaymentSchema.parse({ reason: '  Duplicate charge ' })).toEqual({ reason: 'Duplicate charge' });
  });

  it('rejects an empty or blank reason', () => {
    for (const reason of ['', '   ']) {
      expect(refundPaymentSchema.safeParse({ reason }).error?.issues[0]?.message).toBe(
        'payments:refund.errors.reasonRequired',
      );
    }
  });

  it('rejects a reason over 500 characters', () => {
    const result = refundPaymentSchema.safeParse({ reason: 'a'.repeat(refundReasonMaxLength + 1) });

    expect(result.error?.issues[0]?.message).toBe('payments:refund.errors.reasonTooLong');
  });
});
