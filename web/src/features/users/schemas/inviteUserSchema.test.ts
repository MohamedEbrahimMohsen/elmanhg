import { describe, expect, it } from 'vitest';
import { inviteUserSchema } from './inviteUserSchema';

const message = (values: { displayName: string; email: string; phoneNumber: string }) =>
  inviteUserSchema.safeParse(values).error?.issues[0]?.message;

describe('inviteUserSchema', () => {
  it('accepts valid', () => {
    expect(inviteUserSchema.parse({ displayName: ' Mona ', email: 'mona@example.test', phoneNumber: '' })).toEqual({
      displayName: 'Mona',
      email: 'mona@example.test',
      phoneNumber: '',
    });
  });

  it('requires a name', () => {
    expect(message({ displayName: '  ', email: 'mona@example.test', phoneNumber: '' })).toBe('validation.required');
  });

  it('rejects a name over 100', () => {
    expect(message({ displayName: 'a'.repeat(101), email: 'mona@example.test', phoneNumber: '' })).toBe(
      'users:validation.displayNameLength',
    );
  });

  it('rejects an invalid email', () => {
    expect(message({ displayName: 'Mona', email: 'not-an-email', phoneNumber: '' })).toBe('users:validation.email');
  });

  it('accepts an empty WhatsApp number', () => {
    expect(message({ displayName: 'Mona', email: 'mona@example.test', phoneNumber: '' })).toBeUndefined();
  });

  it('accepts a valid WhatsApp number', () => {
    expect(
      inviteUserSchema.parse({ displayName: 'Mona', email: 'mona@example.test', phoneNumber: '01012345678' })
        .phoneNumber,
    ).toBe('01012345678');
  });

  it('rejects an invalid WhatsApp number', () => {
    expect(message({ displayName: 'Mona', email: 'mona@example.test', phoneNumber: '02012345678' })).toBe(
      'users:validation.phone',
    );
  });
});
