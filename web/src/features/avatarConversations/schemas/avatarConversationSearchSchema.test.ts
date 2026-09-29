import { describe, expect, it } from 'vitest';
import { avatarConversationSearchMaxLength, avatarConversationSearchSchema } from './avatarConversationSearchSchema';

describe('avatarConversationSearchSchema', () => {
  it('parses valid search params', () => {
    expect(
      avatarConversationSearchSchema.parse({
        page: '2',
        search: ' ohm ',
        entryPoint: 'ExamReview',
        from: '2026-10-01',
        to: '2026-10-02',
      }),
    ).toEqual({ page: 2, search: 'ohm', entryPoint: 'ExamReview', from: '2026-10-01', to: '2026-10-02' });
  });

  it('drops an invalid page, entry point, date or overlong search', () => {
    expect(
      avatarConversationSearchSchema.parse({
        page: '0',
        search: 'a'.repeat(avatarConversationSearchMaxLength + 1),
        entryPoint: 'Chat',
        from: '01/10/2026',
        to: 'tomorrow',
      }),
    ).toEqual({ page: undefined, search: undefined, entryPoint: undefined, from: undefined, to: undefined });
  });
});
