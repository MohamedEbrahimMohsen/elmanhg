import { describe, expect, it } from 'vitest';
import { studentDetailSearchSchema } from './studentDetailSearchSchema';

describe('studentDetailSearchSchema', () => {
  it('parses kind and page', () => {
    expect(studentDetailSearchSchema.parse({ kind: 'Exam', page: '2' })).toEqual({ kind: 'Exam', page: 2 });
  });

  it('drops invalid values', () => {
    expect(studentDetailSearchSchema.parse({ kind: 'Homework', page: '-1' })).toEqual({});
  });
});
