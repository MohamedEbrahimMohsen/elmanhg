import { describe, expect, it } from 'vitest';
import { ApiError } from '@/shared/lib/apiError';
import { avatarNoticeOf } from './avatarErrors';

describe('avatarNoticeOf', () => {
  it.each([
    ['AVATAR_EXAM_IN_PROGRESS', 403, 'examInProgress'],
    ['AVATAR_DAILY_LIMIT_REACHED', 403, 'dailyLimit'],
    ['AI_SERVICE_UNAVAILABLE', 503, 'unavailable'],
  ] as const)('maps each avatar error code to its notice', (code, status, notice) => {
    expect(avatarNoticeOf(new ApiError(status, [code], code))).toBe(notice);
  });

  it('maps a missing conversation to the conversation-gone notice', () => {
    expect(avatarNoticeOf(new ApiError(404, ['AVATAR_CONVERSATION_NOT_FOUND'], 'Not found'))).toBe('conversationGone');
  });

  it('falls back to generic for other errors', () => {
    expect(avatarNoticeOf(new ApiError(404, ['LESSON_NOT_FOUND'], 'Not found'))).toBe('generic');
    expect(avatarNoticeOf(new Error('network'))).toBe('generic');
  });
});
