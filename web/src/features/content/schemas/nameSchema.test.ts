import { describe, expect, it } from 'vitest';
import { nameSchema } from './nameSchema';

describe('nameSchema', () => {
  it('accepts a non-empty name', () => {
    expect(nameSchema.safeParse({ name: 'Physics' }).success).toBe(true);
  });

  it('trims surrounding spaces', () => {
    expect(nameSchema.parse({ name: '  Physics ' }).name).toBe('Physics');
  });

  it('rejects an empty name with validation.required', () => {
    expect(nameSchema.safeParse({ name: '' }).error?.issues[0]?.message).toBe('validation.required');
  });

  it('rejects a whitespace-only name', () => {
    expect(nameSchema.safeParse({ name: '   ' }).error?.issues[0]?.message).toBe('validation.required');
  });
});
