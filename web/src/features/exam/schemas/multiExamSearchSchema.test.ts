import { describe, expect, it } from 'vitest';
import { multiExamSearchSchema } from './multiExamSearchSchema';

const subjectId = '11111111-1111-4111-8111-111111111111';
const unitIds = ['22222222-2222-4222-8222-222222222222', '33333333-3333-4333-8333-333333333333'];

describe('multiExamSearchSchema', () => {
  it('accepts a subject, units and size', () => {
    expect(multiExamSearchSchema.parse({ subjectId, unitIds, size: 40 })).toEqual({ subjectId, unitIds, size: 40 });
  });

  it('drops an invalid subject id', () => {
    expect(multiExamSearchSchema.parse({ subjectId: 'physics', unitIds, size: 20 }).subjectId).toBeUndefined();
  });

  it('drops unit ids that are not uuids', () => {
    expect(multiExamSearchSchema.parse({ subjectId, unitIds: ['mechanics'], size: 20 }).unitIds).toBeUndefined();
  });

  it('drops a non-integer size', () => {
    expect(multiExamSearchSchema.parse({ subjectId, unitIds, size: 20.5 }).size).toBeUndefined();
  });
});
