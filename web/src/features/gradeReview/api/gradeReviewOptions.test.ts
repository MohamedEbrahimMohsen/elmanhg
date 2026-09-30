import { describe, expect, it } from 'vitest';
import { kindFromSegment, kindSegments } from './gradeReviewOptions';

describe('gradeReviewOptions', () => {
  it('maps route segments to kinds and back', () => {
    expect(kindFromSegment('essay')).toBe('Essay');
    expect(kindFromSegment('math-steps')).toBe('MathSteps');
    expect(kindSegments.Essay).toBe('essay');
    expect(kindSegments.MathSteps).toBe('math-steps');
  });

  it('returns null for an unknown segment', () => {
    expect(kindFromSegment('Essay')).toBeNull();
    expect(kindFromSegment('drawing')).toBeNull();
  });
});
