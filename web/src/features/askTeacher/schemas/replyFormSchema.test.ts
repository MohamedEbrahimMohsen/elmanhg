import { describe, expect, it } from 'vitest';
import { replyFormSchema } from './replyFormSchema';

describe('replyFormSchema', () => {
  it('accepts a reply', () => {
    expect(replyFormSchema.safeParse({ text: 'Because F = ma.' }).success).toBe(true);
  });

  it('rejects a blank reply', () => {
    const result = replyFormSchema.safeParse({ text: '   ' });

    expect(result.error?.issues.map((issue) => issue.message)).toEqual(['askTeacher:inboxThread.replyRequired']);
  });
});
