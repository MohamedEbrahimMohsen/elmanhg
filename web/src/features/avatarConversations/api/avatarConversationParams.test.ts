import { describe, expect, it } from 'vitest';
import {
  avatarConversationPageSize,
  hasActiveFilters,
  toAvatarConversationPage,
  toAvatarConversationParams,
} from './avatarConversationParams';

describe('avatarConversationParams', () => {
  it('maps the search to request params with local-midnight dates', () => {
    expect(
      toAvatarConversationParams({
        page: 3,
        search: 'ohm',
        entryPoint: 'ExamReview',
        from: '2026-09-10',
        to: '2026-09-30',
      }),
    ).toEqual({
      pageNumber: 3,
      pageSize: avatarConversationPageSize,
      search: 'ohm',
      entryPoint: 'ExamReview',
      from: new Date(2026, 8, 10).toISOString(),
      to: new Date(2026, 9, 1).toISOString(),
    });
  });

  it('omits empty filters and defaults the page', () => {
    expect(toAvatarConversationParams({})).toEqual({ pageNumber: 1, pageSize: avatarConversationPageSize });
  });

  it('reports active filters', () => {
    expect(hasActiveFilters({ page: 2 })).toBe(false);
    expect(hasActiveFilters({ search: 'ohm' })).toBe(true);
    expect(hasActiveFilters({ entryPoint: 'Global' })).toBe(true);
    expect(hasActiveFilters({ from: '2026-09-01' })).toBe(true);
    expect(hasActiveFilters({ to: '2026-09-01' })).toBe(true);
  });

  it('defaults missing page fields', () => {
    expect(toAvatarConversationPage({})).toEqual({ items: [], pageNumber: 1, totalPages: 0, totalItems: 0 });
    expect(toAvatarConversationPage({ items: [], pageNumber: '2', totalItems: '41', totalPages: '3' })).toEqual({
      items: [],
      pageNumber: 2,
      totalPages: 3,
      totalItems: 41,
    });
  });
});
