import { describe, expect, it } from 'vitest';
import { questionListSearchSchema } from './questionListSearchSchema';

const lessonId = '11111111-1111-4111-8111-111111111111';
const teacherId = '22222222-2222-4222-8222-222222222222';
const subjectId = '33333333-3333-4333-8333-333333333333';

describe('questionListSearchSchema', () => {
  it('parses every filter', () => {
    const search = {
      page: 2,
      status: 'Rejected',
      type: 'Fill',
      subjectId,
      lessonId,
      teacherId,
      minVersion: 2,
      rejectionReason: 'unit',
    };

    expect(questionListSearchSchema.parse(search)).toEqual(search);
  });

  it('drops invalid values', () => {
    const search = questionListSearchSchema.parse({ status: 'Foo', page: 0, lessonId: 'x', minVersion: 0 });

    expect(search).toEqual({
      status: undefined,
      page: undefined,
      lessonId: undefined,
      minVersion: undefined,
      type: undefined,
      subjectId: undefined,
      teacherId: undefined,
      rejectionReason: undefined,
    });
  });
});
