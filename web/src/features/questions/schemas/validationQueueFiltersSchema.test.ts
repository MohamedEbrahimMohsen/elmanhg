import { describe, expect, it } from 'vitest';
import { validationQueueFiltersSchema } from './validationQueueFiltersSchema';

const empty = { unitId: '', lessonId: '', type: '', difficulty: '', minAgeDays: '' };

describe('validationQueueFiltersSchema', () => {
  it('accepts empty filters', () => {
    expect(validationQueueFiltersSchema.safeParse(empty).success).toBe(true);
  });

  it('rejects an unsupported age', () => {
    expect(validationQueueFiltersSchema.safeParse({ ...empty, minAgeDays: '5' }).success).toBe(false);
  });
});
