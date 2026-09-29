import { describe, expect, it } from 'vitest';
import { followUpFormSchema } from './followUpFormSchema';

describe('followUpFormSchema', () => {
  it('accepts a follow-up with text', () => {
    expect(followUpFormSchema.safeParse({ text: 'Can you show the units?' }).success).toBe(true);
  });

  it('rejects a blank follow-up', () => {
    const result = followUpFormSchema.safeParse({ text: '   ' });

    expect(result.error?.issues.map((issue) => issue.message)).toEqual(['askTeacher:followUp.required']);
  });
});
