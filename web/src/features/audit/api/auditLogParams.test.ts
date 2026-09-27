import { describe, expect, it } from 'vitest';
import { auditLogPageSize, hasActiveFilters, toAuditLogPage, toAuditLogParams } from './auditLogParams';

describe('auditLogParams', () => {
  it('maps page and page size', () => {
    expect(toAuditLogParams({ page: 3 })).toEqual({ pageNumber: 3, pageSize: auditLogPageSize });
    expect(toAuditLogParams({}).pageNumber).toBe(1);
  });

  it('sends from as local midnight', () => {
    expect(toAuditLogParams({ from: '2026-09-10' }).from).toBe(new Date(2026, 8, 10).toISOString());
  });

  it('sends to as the next local midnight', () => {
    expect(toAuditLogParams({ to: '2026-09-30' }).to).toBe(new Date(2026, 8, 31).toISOString());
  });

  it('omits unset filters', () => {
    const params = toAuditLogParams({ actor: 'admin', resourceType: 'Teacher' });

    expect(params).toEqual({ pageNumber: 1, pageSize: auditLogPageSize, actor: 'admin', resourceType: 'Teacher' });
    expect(Object.keys(params)).not.toContain('from');
    expect(Object.keys(params)).not.toContain('to');
  });

  it('detects active filters', () => {
    expect(hasActiveFilters({ page: 2 })).toBe(false);
    expect(hasActiveFilters({ resourceType: 'Teacher' })).toBe(true);
    expect(hasActiveFilters({ to: '2026-09-01' })).toBe(true);
  });

  it('normalises string paging numbers', () => {
    expect(toAuditLogPage({ items: [], pageNumber: '2', pageSize: '20', totalItems: '41', totalPages: '3' })).toEqual({
      items: [],
      pageNumber: 2,
      totalPages: 3,
      totalItems: 41,
    });
  });
});
