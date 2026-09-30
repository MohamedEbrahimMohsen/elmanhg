import { describe, expect, it } from 'vitest';
import { toReviewRequest } from './toReviewRequest';

describe('toReviewRequest', () => {
  it('sends a null score and a trimmed comment for accept', () => {
    expect(toReviewRequest({ decision: 'Accepted', score: '3', comment: '  Fair.  ' })).toEqual({
      decision: 'Accepted',
      score: null,
      comment: 'Fair.',
    });
  });

  it('sends a number and the comment for override', () => {
    expect(toReviewRequest({ decision: 'Overridden', score: ' 3.5 ', comment: 'Good example.' })).toEqual({
      decision: 'Overridden',
      score: 3.5,
      comment: 'Good example.',
    });
  });

  it('sends a null comment when blank', () => {
    expect(toReviewRequest({ decision: 'Accepted', score: '', comment: '   ' }).comment).toBeNull();
  });
});
