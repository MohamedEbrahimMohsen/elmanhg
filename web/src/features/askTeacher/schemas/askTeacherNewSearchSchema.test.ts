import { describe, expect, it } from 'vitest';
import { askTeacherNewSearchSchema } from './askTeacherNewSearchSchema';

const attemptId = '22222222-2222-4222-8222-222222222222';

describe('askTeacherNewSearchSchema', () => {
  it('keeps a valid attempt id', () => {
    expect(askTeacherNewSearchSchema.parse({ attemptId })).toEqual({ attemptId });
  });

  it('drops a malformed lesson id', () => {
    expect(askTeacherNewSearchSchema.parse({ lessonId: 'not-a-guid' })).toEqual({ lessonId: undefined });
  });
});
