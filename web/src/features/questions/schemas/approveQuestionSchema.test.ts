import { describe, expect, it } from 'vitest';
import { approveQuestionSchema } from './approveQuestionSchema';

describe('approveQuestionSchema', () => {
  it('accepts a known difficulty', () => {
    expect(approveQuestionSchema.parse({ difficulty: 'Hard' })).toEqual({ difficulty: 'Hard' });
  });

  it('rejects an unknown difficulty with the error key', () => {
    const result = approveQuestionSchema.safeParse({ difficulty: 'Extreme' });

    expect(result.error?.issues[0]?.message).toBe('errors.QUESTION_DIFFICULTY_INVALID');
  });
});
