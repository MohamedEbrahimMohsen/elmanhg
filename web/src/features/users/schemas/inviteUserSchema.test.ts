import { describe, expect, it } from 'vitest';
import { inviteUserSchema } from './inviteUserSchema';

const message = (values: { displayName: string; email: string }) =>
  inviteUserSchema.safeParse(values).error?.issues[0]?.message;

describe('inviteUserSchema', () => {
  it('accepts valid', () => {
    expect(inviteUserSchema.parse({ displayName: ' Mona ', email: 'mona@example.test' })).toEqual({
      displayName: 'Mona',
      email: 'mona@example.test',
    });
  });

  it('requires a name', () => {
    expect(message({ displayName: '  ', email: 'mona@example.test' })).toBe('validation.required');
  });

  it('rejects a name over 100', () => {
    expect(message({ displayName: 'a'.repeat(101), email: 'mona@example.test' })).toBe(
      'users:validation.displayNameLength',
    );
  });

  it('rejects an invalid email', () => {
    expect(message({ displayName: 'Mona', email: 'not-an-email' })).toBe('users:validation.email');
  });
});
