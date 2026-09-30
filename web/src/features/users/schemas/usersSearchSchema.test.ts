import { describe, expect, it } from 'vitest';
import { userSearchMaxLength, usersSearchSchema } from './usersSearchSchema';

describe('usersSearchSchema', () => {
  it('parses a valid tab, q, status and page', () => {
    expect(usersSearchSchema.parse({ tab: 'teachers', q: 'Mona', status: 'Suspended', page: '3' })).toEqual({
      tab: 'teachers',
      q: 'Mona',
      status: 'Suspended',
      page: 3,
    });
  });

  it('drops an unknown tab, status and page', () => {
    expect(usersSearchSchema.parse({ tab: 'parents', status: 'Deleted', page: '0' })).toEqual({});
  });

  it('trims q', () => {
    expect(usersSearchSchema.parse({ q: '  01012345678 ' })).toEqual({ q: '01012345678' });
  });

  it('drops q longer than 256', () => {
    expect(usersSearchSchema.parse({ q: 'a'.repeat(userSearchMaxLength + 1) })).toEqual({});
  });
});
