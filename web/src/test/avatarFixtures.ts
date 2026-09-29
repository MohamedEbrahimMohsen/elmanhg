import type { AvatarReplyResult, AvatarStatusResult } from '@/shared/api/generated/model';
import { browseLessonId } from '@/test/browseFixtures';

export function avatarStatus(overrides?: Partial<AvatarStatusResult>): AvatarStatusResult {
  return {
    examInProgress: false,
    tier: 'Base',
    dailyMessageLimit: 50,
    messagesUsedToday: 0,
    messagesRemainingToday: 50,
    messageMaxLength: 2000,
    maxHistoryMessages: 10,
    ...overrides,
  };
}

export function freeAvatarStatus(overrides?: Partial<AvatarStatusResult>): AvatarStatusResult {
  return avatarStatus({ tier: 'Free', dailyMessageLimit: 5, messagesRemainingToday: 5, ...overrides });
}

export function avatarReply(overrides?: Partial<AvatarReplyResult>): AvatarReplyResult {
  return {
    reply: 'المقاومة = فرق الجهد ÷ شدة التيار.',
    citations: [
      {
        reference: 'explanation-1',
        section: 'Explanation',
        sectionTitle: 'قانون أوم',
        lessonId: browseLessonId,
        questionId: null,
      },
    ],
    dailyMessageLimit: 50,
    messagesUsedToday: 1,
    messagesRemainingToday: 49,
    model: 'fake',
    promptVersion: 'fake',
    ...overrides,
  };
}
