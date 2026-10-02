import { ApiError } from '@/shared/lib/apiError';

export type AvatarNoticeKind = 'examInProgress' | 'dailyLimit' | 'unavailable' | 'conversationGone' | 'generic';

const noticeByCode: Record<string, AvatarNoticeKind> = {
  AVATAR_EXAM_IN_PROGRESS: 'examInProgress',
  AVATAR_DAILY_LIMIT_REACHED: 'dailyLimit',
  AI_SERVICE_UNAVAILABLE: 'unavailable',
  AVATAR_CONVERSATION_NOT_FOUND: 'conversationGone',
};

export function avatarNoticeOf(error: unknown): AvatarNoticeKind {
  return error instanceof ApiError ? (noticeByCode[error.code] ?? 'generic') : 'generic';
}
