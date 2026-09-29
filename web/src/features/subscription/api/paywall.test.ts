import { describe, expect, it } from 'vitest';
import { paywallReason } from './paywall';

describe('paywallReason', () => {
  it('maps each paywall code to its reason', () => {
    expect(paywallReason('QUIZ_DAILY_LIMIT_REACHED')).toBe('dailyQuiz');
    expect(paywallReason('LESSON_LOCKED')).toBe('lesson');
    expect(paywallReason('EXAM_REQUIRES_SUBSCRIPTION')).toBe('exam');
  });

  it('returns null for other codes and for null', () => {
    expect(paywallReason('SESSION_NOT_FOUND')).toBeNull();
    expect(paywallReason(null)).toBeNull();
  });
});
