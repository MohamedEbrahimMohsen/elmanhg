import { describe, expect, it } from 'vitest';
import { userFiltersSchema } from './userFiltersSchema';
import { userSearchMaxLength } from './usersSearchSchema';

describe('userFiltersSchema', () => {
  it('accepts empty filters', () => {
    expect(userFiltersSchema.parse({ q: '', status: '' })).toEqual({ q: '', status: '' });
  });

  it('rejects a search over 256 with users:validation.searchTooLong', () => {
    const result = userFiltersSchema.safeParse({ q: 'a'.repeat(userSearchMaxLength + 1), status: 'Active' });

    expect(result.error?.issues[0]?.message).toBe('users:validation.searchTooLong');
  });
});
