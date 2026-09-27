import { describe, expect, it } from 'vitest';
import { auditLogSearchSchema } from './auditLogSearchSchema';

describe('auditLogSearchSchema', () => {
  it('keeps valid filters and page', () => {
    expect(
      auditLogSearchSchema.parse({
        page: '2',
        actor: ' admin@elmanhg.test ',
        resourceType: 'Teacher',
        from: '2026-09-01',
        to: '2026-09-10',
      }),
    ).toEqual({ page: 2, actor: 'admin@elmanhg.test', resourceType: 'Teacher', from: '2026-09-01', to: '2026-09-10' });
  });

  it('drops an invalid page', () => {
    expect(auditLogSearchSchema.parse({ page: '0' }).page).toBeUndefined();
  });

  it('drops an invalid date', () => {
    expect(auditLogSearchSchema.parse({ from: '2026-13-40' }).from).toBeUndefined();
  });

  it('drops a blank actor', () => {
    expect(auditLogSearchSchema.parse({ actor: '   ' }).actor).toBeUndefined();
  });

  it('accepts an empty search', () => {
    expect(auditLogSearchSchema.parse({})).toEqual({
      page: undefined,
      actor: undefined,
      resourceType: undefined,
      from: undefined,
      to: undefined,
    });
  });
});
