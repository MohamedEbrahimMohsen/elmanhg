import { describe, expect, it } from 'vitest';
import { hasActiveFilters, paymentLogPageSize, toPaymentLogParams } from './paymentLogParams';

describe('toPaymentLogParams', () => {
  it('maps the search to request params', () => {
    expect(toPaymentLogParams({})).toEqual({ pageNumber: 1, pageSize: paymentLogPageSize, needsReview: false });
    expect(
      toPaymentLogParams({
        page: 3,
        view: 'review',
        status: 'Refunded',
        plan: 'AskTeacher',
        reference: '192036465',
        studentId: 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7',
        from: '2026-09-01',
        to: '2026-09-10',
      }),
    ).toEqual({
      pageNumber: 3,
      pageSize: 20,
      needsReview: true,
      status: 'Refunded',
      plan: 'AskTeacher',
      reference: '192036465',
      studentId: 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7',
      from: new Date(2026, 8, 1).toISOString(),
      to: new Date(2026, 8, 11).toISOString(),
    });
  });
});

describe('hasActiveFilters', () => {
  it('reports active filters', () => {
    expect(hasActiveFilters({ status: 'Failed' })).toBe(true);
    expect(hasActiveFilters({ plan: 'Base' })).toBe(true);
    expect(hasActiveFilters({ reference: 'txn' })).toBe(true);
    expect(hasActiveFilters({ from: '2026-09-01' })).toBe(true);
    expect(hasActiveFilters({ to: '2026-09-01' })).toBe(true);
    expect(hasActiveFilters({ studentId: 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7' })).toBe(true);
    expect(hasActiveFilters({ view: 'review', page: 2 })).toBe(false);
  });
});
