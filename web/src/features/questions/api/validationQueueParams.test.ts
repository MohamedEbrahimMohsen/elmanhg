import { describe, expect, it } from 'vitest';
import { hasActiveValidationFilters, toValidationQueueParams } from './validationQueueParams';

const unitId = '11111111-1111-4111-8111-111111111111';
const lessonId = '22222222-2222-4222-8222-222222222222';
const sessionId = '33333333-3333-4333-8333-333333333333';

describe('validationQueueParams', () => {
  it('maps filters and the session id', () => {
    expect(
      toValidationQueueParams({ page: 2, unitId, lessonId, type: 'Mcq', difficulty: 'Hard', minAgeDays: 7 }, sessionId),
    ).toEqual({
      pageNumber: 2,
      pageSize: 20,
      unitId,
      lessonId,
      type: 'Mcq',
      difficulty: 'Hard',
      minAgeDays: 7,
      reviewSessionId: sessionId,
    });
  });

  it('omits empty filters', () => {
    expect(toValidationQueueParams({}, sessionId)).toEqual({
      pageNumber: 1,
      pageSize: 20,
      reviewSessionId: sessionId,
    });
  });

  it('detects active filters', () => {
    expect(hasActiveValidationFilters({ page: 3 })).toBe(false);
    expect(hasActiveValidationFilters({ minAgeDays: 1 })).toBe(true);
    expect(hasActiveValidationFilters({ unitId })).toBe(true);
  });
});
