import { describe, expect, it } from 'vitest';
import { emailSignInSchema } from './emailSignInSchema';

describe('emailSignInSchema', () => {
  it('accepts an email and password', () => {
    expect(emailSignInSchema.safeParse({ email: 'admin@elmanhg.test', password: 'x' }).success).toBe(true);
  });

  it('rejects a malformed email', () => {
    expect(emailSignInSchema.safeParse({ email: 'admin', password: 'x' }).error?.issues[0]?.message).toBe(
      'session:validation.email',
    );
  });

  it('requires a password', () => {
    expect(emailSignInSchema.safeParse({ email: 'admin@elmanhg.test', password: '' }).error?.issues[0]?.message).toBe(
      'validation.required',
    );
  });
});
