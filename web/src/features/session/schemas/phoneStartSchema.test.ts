import { describe, expect, it } from 'vitest';
import { phoneSignInSchema, phoneSignUpSchema } from './phoneStartSchema';

const firstMessage = (result: { success: boolean; error?: { issues: { message: string }[] } }) =>
  result.error?.issues[0]?.message;

describe('phoneStartSchema', () => {
  it('accepts a valid Egyptian mobile number', () => {
    expect(phoneSignInSchema.safeParse({ phoneNumber: '01512345678' }).success).toBe(true);
  });

  it('rejects a number with an unknown prefix', () => {
    expect(firstMessage(phoneSignInSchema.safeParse({ phoneNumber: '01312345678' }))).toBe('session:validation.phone');
  });

  it('rejects a number with 10 digits', () => {
    expect(firstMessage(phoneSignInSchema.safeParse({ phoneNumber: '0101234567' }))).toBe('session:validation.phone');
  });

  it('requires a display name on sign-up', () => {
    expect(firstMessage(phoneSignUpSchema.safeParse({ phoneNumber: '01012345678', displayName: '  ' }))).toBe(
      'validation.required',
    );
  });

  it('rejects a display name over 100 characters', () => {
    expect(
      firstMessage(phoneSignUpSchema.safeParse({ phoneNumber: '01012345678', displayName: 'a'.repeat(101) })),
    ).toBe('session:validation.displayNameLength');
  });
});
