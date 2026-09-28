import { describe, expect, it } from 'vitest';
import { validationQueueSearchSchema } from './validationQueueSearchSchema';

const unitId = '11111111-1111-4111-8111-111111111111';
const lessonId = '22222222-2222-4222-8222-222222222222';

describe('validationQueueSearchSchema', () => {
  it('accepts valid filters', () => {
    const search = { page: 2, unitId, lessonId, type: 'Fill', difficulty: 'Hard', minAgeDays: 3 };

    expect(validationQueueSearchSchema.parse(search)).toEqual(search);
  });

  it('drops an invalid guid', () => {
    expect(validationQueueSearchSchema.parse({ unitId: 'not-a-guid', lessonId }).unitId).toBeUndefined();
  });

  it('drops an age that is not offered', () => {
    expect(validationQueueSearchSchema.parse({ minAgeDays: 5 }).minAgeDays).toBeUndefined();
  });

  it('coerces the page number', () => {
    expect(validationQueueSearchSchema.parse({ page: '3' }).page).toBe(3);
  });
});
