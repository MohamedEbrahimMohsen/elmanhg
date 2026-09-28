import { describe, expect, it } from 'vitest';
import { hasActiveFilters, toQuestionListPage, toQuestionListParams } from './questionListParams';
import { questionListPageSize } from './questionOptions';

const lessonId = '11111111-1111-4111-8111-111111111111';

describe('questionListParams', () => {
  it('maps page and page size', () => {
    expect(toQuestionListParams({})).toEqual({ pageNumber: 1, pageSize: questionListPageSize });
    expect(toQuestionListParams({ page: 3 }).pageNumber).toBe(3);
  });

  it('omits unset filters', () => {
    expect(toQuestionListParams({ status: 'Rejected', lessonId, minVersion: 2 })).toEqual({
      pageNumber: 1,
      pageSize: 20,
      status: 'Rejected',
      lessonId,
      minVersion: 2,
    });
  });

  it('detects active filters', () => {
    expect(hasActiveFilters({ page: 2 })).toBe(false);
    expect(hasActiveFilters({ lessonId })).toBe(true);
  });

  it('coerces page numbers', () => {
    expect(toQuestionListPage({ items: [], pageNumber: '2', totalPages: '3', totalItems: '41' })).toEqual({
      items: [],
      pageNumber: 2,
      totalPages: 3,
      totalItems: 41,
    });
  });
});
