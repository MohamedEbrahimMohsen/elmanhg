import { describe, expect, it } from 'vitest';
import { emailCodeStartSchema } from './emailCodeStartSchema';

describe('emailCodeStartSchema', () => {
  it('accepts a valid email', () => {
    expect(emailCodeStartSchema.safeParse({ email: 'mona@elmanhg.test' }).success).toBe(true);
  });

  it('rejects a malformed email', () => {
    expect(emailCodeStartSchema.safeParse({ email: 'mona' }).error?.issues[0]?.message).toBe(
      'session:validation.email',
    );
  });
});
