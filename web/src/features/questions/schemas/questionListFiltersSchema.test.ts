import { describe, expect, it } from 'vitest';
import { questionFilterMaxLength } from '../api/questionOptions';
import { questionListFiltersSchema } from './questionListFiltersSchema';

const blank = { status: '', type: '', subjectId: '', teacherId: '', minVersion: '', rejectionReason: '' };

describe('questionListFiltersSchema', () => {
  it('accepts empty filters', () => {
    expect(questionListFiltersSchema.safeParse(blank).success).toBe(true);
  });

  it.each(['0', 'abc'])('rejects a minimum version that is not a positive whole number', (minVersion) => {
    const result = questionListFiltersSchema.safeParse({ ...blank, minVersion });

    expect(result.error?.issues[0]?.message).toBe('questions:list.filters.errors.minVersion');
  });

  it('rejects a rejection-reason search over 200 characters', () => {
    const result = questionListFiltersSchema.safeParse({
      ...blank,
      rejectionReason: 'a'.repeat(questionFilterMaxLength + 1),
    });

    expect(result.error?.issues[0]?.message).toBe('questions:list.filters.errors.rejectionReasonTooLong');
  });
});
