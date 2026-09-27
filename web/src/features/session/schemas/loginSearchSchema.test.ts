import { describe, expect, it } from 'vitest';
import { loginSearchSchema } from './loginSearchSchema';

describe('loginSearchSchema', () => {
  it('keeps an internal redirect path', () => {
    expect(loginSearchSchema.parse({ redirect: '/admin/users' })).toEqual({ redirect: '/admin/users' });
  });

  it('accepts a missing redirect', () => {
    expect(loginSearchSchema.parse({})).toEqual({ redirect: undefined });
  });

  it('drops an absolute external URL', () => {
    expect(loginSearchSchema.parse({ redirect: 'https://evil.example' }).redirect).toBeUndefined();
  });

  it('drops a protocol-relative URL', () => {
    expect(loginSearchSchema.parse({ redirect: '//evil.example' }).redirect).toBeUndefined();
  });
});
