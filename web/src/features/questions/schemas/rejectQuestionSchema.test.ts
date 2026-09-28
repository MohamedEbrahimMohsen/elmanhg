import { describe, expect, it } from 'vitest';
import { rejectionReasonMaxLength } from '../api/questionOptions';
import { rejectQuestionSchema } from './rejectQuestionSchema';

describe('rejectQuestionSchema', () => {
  it('accepts a reason', () => {
    expect(rejectQuestionSchema.parse({ reason: '  Wrong unit  ' })).toEqual({ reason: 'Wrong unit' });
  });

  it('rejects a blank reason with the required key', () => {
    const result = rejectQuestionSchema.safeParse({ reason: '   ' });

    expect(result.error?.issues[0]?.message).toBe('errors.QUESTION_REJECTION_REASON_REQUIRED');
  });

  it('rejects a reason over the maximum with the too-long key', () => {
    const result = rejectQuestionSchema.safeParse({ reason: 'a'.repeat(rejectionReasonMaxLength + 1) });

    expect(result.error?.issues[0]?.message).toBe('errors.QUESTION_REJECTION_REASON_TOO_LONG');
  });
});
