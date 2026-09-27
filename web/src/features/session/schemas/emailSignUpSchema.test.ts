import { describe, expect, it } from 'vitest';
import { emailSignUpSchema } from './emailSignUpSchema';

const valid = { displayName: 'Mona', email: 'mona@elmanhg.test', password: 'Password1' };

const firstMessage = (values: typeof valid) => emailSignUpSchema.safeParse(values).error?.issues[0]?.message;

describe('emailSignUpSchema', () => {
  it('accepts a name, email and strong password', () => {
    expect(emailSignUpSchema.safeParse(valid).success).toBe(true);
  });

  it('rejects a password shorter than 8 characters', () => {
    expect(firstMessage({ ...valid, password: 'Pass1' })).toBe('session:validation.passwordLength');
  });

  it('rejects a password without a digit', () => {
    expect(firstMessage({ ...valid, password: 'Password' })).toBe('session:validation.passwordDigit');
  });

  it('rejects a malformed email', () => {
    expect(firstMessage({ ...valid, email: 'mona' })).toBe('session:validation.email');
  });

  it('requires a display name', () => {
    expect(firstMessage({ ...valid, displayName: '' })).toBe('validation.required');
  });
});
