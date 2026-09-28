import { describe, expect, it } from 'vitest';
import { blueprintsSearchSchema } from './blueprintsSearchSchema';

describe('blueprintsSearchSchema', () => {
  it('keeps a valid subject id', () => {
    const subjectId = '11111111-1111-4111-8111-111111111111';

    expect(blueprintsSearchSchema.parse({ subjectId }).subjectId).toBe(subjectId);
  });

  it('drops an invalid one', () => {
    expect(blueprintsSearchSchema.parse({ subjectId: 'physics' }).subjectId).toBeUndefined();
  });
});
