import { describe, expect, it } from 'vitest';
import { setPasswordSchema } from './setPasswordSchema';

const issue = (password: string, confirmPassword: string) =>
  setPasswordSchema.safeParse({ password, confirmPassword }).error?.issues[0];

describe('setPasswordSchema', () => {
  it('accepts matching valid passwords', () => {
    expect(setPasswordSchema.safeParse({ password: 'Invited2026', confirmPassword: 'Invited2026' }).success).toBe(true);
  });

  it('rejects short', () => {
    expect(issue('Ab1', 'Ab1')?.message).toBe('session:validation.passwordLength');
  });

  it('rejects without a digit', () => {
    expect(issue('Passwords', 'Passwords')?.message).toBe('session:validation.passwordDigit');
  });

  it('rejects a mismatch on confirmPassword', () => {
    const mismatch = issue('Invited2026', 'Invited2027');

    expect(mismatch?.message).toBe('session:validation.passwordMismatch');
    expect(mismatch?.path).toEqual(['confirmPassword']);
  });
});
