import { describe, expect, it } from 'vitest';
import { avatarMessageSchema } from './avatarMessageSchema';

const schema = avatarMessageSchema(10);

const errorOf = (message: string) => schema.safeParse({ message }).error?.issues[0]?.message;

describe('avatarMessageSchema', () => {
  it('accepts a question', () => {
    expect(schema.safeParse({ message: '  ما أوم؟  ' })).toEqual({ success: true, data: { message: 'ما أوم؟' } });
  });

  it('rejects an empty question', () => {
    expect(errorOf('   ')).toBe('avatar:composer.required');
  });

  it('rejects a question over the limit', () => {
    expect(errorOf('x'.repeat(11))).toBe('avatar:composer.tooLong');
  });
});
