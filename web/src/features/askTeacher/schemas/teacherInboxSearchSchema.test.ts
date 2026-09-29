import { describe, expect, it } from 'vitest';
import { teacherInboxSearchSchema } from './teacherInboxSearchSchema';

describe('teacherInboxSearchSchema', () => {
  it('accepts the Unclaimed and Mine filters', () => {
    expect(teacherInboxSearchSchema.parse({ filter: 'Unclaimed' })).toEqual({ filter: 'Unclaimed' });
    expect(teacherInboxSearchSchema.parse({ filter: 'Mine' })).toEqual({ filter: 'Mine' });
  });

  it('drops an unknown filter', () => {
    expect(teacherInboxSearchSchema.parse({ filter: 'Everything' })).toEqual({ filter: undefined });
  });

  it('coerces the page and drops an invalid one', () => {
    expect(teacherInboxSearchSchema.parse({ page: '2' })).toEqual({ page: 2 });
    expect(teacherInboxSearchSchema.parse({ page: '0' })).toEqual({ page: undefined });
  });
});
