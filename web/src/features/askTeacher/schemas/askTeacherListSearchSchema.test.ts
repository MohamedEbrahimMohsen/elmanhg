import { describe, expect, it } from 'vitest';
import { askTeacherListSearchSchema } from './askTeacherListSearchSchema';

describe('askTeacherListSearchSchema', () => {
  it('parses a page number', () => {
    expect(askTeacherListSearchSchema.parse({ page: '3' })).toEqual({ page: 3 });
  });

  it('drops an invalid page', () => {
    expect(askTeacherListSearchSchema.parse({ page: '0' })).toEqual({ page: undefined });
  });
});
