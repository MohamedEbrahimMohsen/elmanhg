import { describe, expect, it } from 'vitest';
import { teacherPhoneSchema } from './teacherPhoneSchema';

describe('teacherPhoneSchema', () => {
  it('accepts a valid number', () => {
    expect(teacherPhoneSchema.parse({ phoneNumber: ' 01512345678 ' })).toEqual({ phoneNumber: '01512345678' });
  });

  it('rejects a short number', () => {
    expect(teacherPhoneSchema.safeParse({ phoneNumber: '0101234' }).error?.issues[0]?.message).toBe(
      'users:validation.phone',
    );
  });
});
