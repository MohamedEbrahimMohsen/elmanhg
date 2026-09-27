import { describe, expect, it } from 'vitest';
import { auditLogFiltersSchema } from './auditLogFiltersSchema';
import { auditLogFilterMaxLength } from './auditLogSearchSchema';

const blank = { actor: '', resourceType: '', from: '', to: '' };

describe('auditLogFiltersSchema', () => {
  it('accepts blank filters', () => {
    expect(auditLogFiltersSchema.safeParse(blank).success).toBe(true);
  });

  it('rejects an actor over the limit', () => {
    const result = auditLogFiltersSchema.safeParse({ ...blank, actor: 'a'.repeat(auditLogFilterMaxLength + 1) });

    expect(result.error?.issues[0]?.message).toBe('audit:filters.errors.actorTooLong');
  });

  it('rejects an end date before the start date', () => {
    const result = auditLogFiltersSchema.safeParse({ ...blank, from: '2026-09-10', to: '2026-09-01' });

    expect(result.error?.issues[0]?.message).toBe('audit:filters.errors.dateRange');
    expect(result.error?.issues[0]?.path).toEqual(['to']);
  });

  it('accepts the same start and end date', () => {
    expect(auditLogFiltersSchema.safeParse({ ...blank, from: '2026-09-10', to: '2026-09-10' }).success).toBe(true);
  });

  it('rejects a malformed date', () => {
    const result = auditLogFiltersSchema.safeParse({ ...blank, from: '10/09/2026' });

    expect(result.error?.issues.map((issue) => issue.message)).toContain('audit:filters.errors.date');
  });
});
