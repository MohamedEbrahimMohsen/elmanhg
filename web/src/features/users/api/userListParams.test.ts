import { describe, expect, it } from 'vitest';
import { hasActiveFilters, toGetUsersParams } from './userListParams';

describe('userListParams', () => {
  it('maps the teachers tab to the Teacher role with search and status', () => {
    expect(toGetUsersParams({ tab: 'teachers', q: 'Mona', status: 'Suspended', page: 2 })).toEqual({
      role: 'Teacher',
      pageNumber: 2,
      pageSize: 20,
      search: 'Mona',
      status: 'Suspended',
    });
  });

  it('defaults to Student page 1', () => {
    expect(toGetUsersParams({})).toEqual({ role: 'Student', pageNumber: 1, pageSize: 20 });
  });

  it('hasActiveFilters is true only with q or status', () => {
    expect(hasActiveFilters({ tab: 'admins', page: 3 })).toBe(false);
    expect(hasActiveFilters({ q: 'Mona' })).toBe(true);
    expect(hasActiveFilters({ status: 'Active' })).toBe(true);
  });
});
