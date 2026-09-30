import { describe, expect, it } from 'vitest';
import { dashboardSearchSchema } from './dashboardSearchSchema';

describe('dashboardSearchSchema', () => {
  it('keeps each allowed period', () => {
    expect(dashboardSearchSchema.parse({ days: 7 }).days).toBe(7);
    expect(dashboardSearchSchema.parse({ days: 14 }).days).toBe(14);
    expect(dashboardSearchSchema.parse({ days: 30 }).days).toBe(30);
  });

  it('drops a period that is not offered', () => {
    expect(dashboardSearchSchema.parse({ days: 5 }).days).toBeUndefined();
  });

  it('keeps a valid subject id and drops an invalid one', () => {
    const subjectId = '11111111-1111-4111-8111-111111111111';

    expect(dashboardSearchSchema.parse({ subjectId }).subjectId).toBe(subjectId);
    expect(dashboardSearchSchema.parse({ subjectId: 'physics' }).subjectId).toBeUndefined();
  });

  it('accepts no params', () => {
    expect(dashboardSearchSchema.parse({})).toEqual({});
  });
});
