import { describe, expect, it } from 'vitest';
import { otpSchema } from './otpSchema';

describe('otpSchema', () => {
  it('accepts six digits', () => {
    expect(otpSchema.safeParse({ code: '123456' }).success).toBe(true);
  });

  it('rejects five digits', () => {
    expect(otpSchema.safeParse({ code: '12345' }).error?.issues[0]?.message).toBe('session:validation.code');
  });

  it('rejects letters', () => {
    expect(otpSchema.safeParse({ code: '12a456' }).error?.issues[0]?.message).toBe('session:validation.code');
  });
});
