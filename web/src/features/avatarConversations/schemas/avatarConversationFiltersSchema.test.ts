import { describe, expect, it } from 'vitest';
import { avatarConversationFiltersSchema } from './avatarConversationFiltersSchema';
import { avatarConversationSearchMaxLength } from './avatarConversationSearchSchema';

const blank = { search: '', entryPoint: '', from: '', to: '' };

describe('avatarConversationFiltersSchema', () => {
  it('accepts blank filters', () => {
    expect(avatarConversationFiltersSchema.safeParse(blank).success).toBe(true);
  });

  it('rejects a search over the limit', () => {
    const result = avatarConversationFiltersSchema.safeParse({
      ...blank,
      search: 'a'.repeat(avatarConversationSearchMaxLength + 1),
    });

    expect(result.error?.issues[0]?.message).toBe('avatarConversations:filters.errors.searchTooLong');
  });

  it('rejects an invalid date', () => {
    const result = avatarConversationFiltersSchema.safeParse({ ...blank, from: '01/10/2026' });

    expect(result.error?.issues[0]?.message).toBe('avatarConversations:filters.errors.date');
  });

  it('rejects an end date before the start', () => {
    const result = avatarConversationFiltersSchema.safeParse({ ...blank, from: '2026-10-10', to: '2026-10-01' });

    expect(result.error?.issues[0]?.message).toBe('avatarConversations:filters.errors.dateRange');
    expect(result.error?.issues[0]?.path).toEqual(['to']);
  });

  it('accepts the same start and end day', () => {
    expect(avatarConversationFiltersSchema.safeParse({ ...blank, from: '2026-10-10', to: '2026-10-10' }).success).toBe(
      true,
    );
  });
});
