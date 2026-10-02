import type {
  AvatarReplyResult,
  AvatarStatusResult,
  PageDataOfStudentAvatarConversationResult,
  StudentAvatarConversationDetailResult,
  StudentAvatarConversationResult,
} from '@/shared/api/generated/model';
import { browseLessonId } from '@/test/browseFixtures';

export const avatarConversationId = '7d1c2b3a-0000-4000-8000-00000000c0de';
export const myAvatarConversationId = '7d1c2b3a-0000-4000-8000-0000000c0de2';

export function avatarStatus(overrides?: Partial<AvatarStatusResult>): AvatarStatusResult {
  return {
    examInProgress: false,
    tier: 'Base',
    dailyMessageLimit: 50,
    messagesUsedToday: 0,
    messagesRemainingToday: 50,
    messageMaxLength: 2000,
    maxHistoryMessages: 10,
    conversationDeletionEnabled: true,
    ...overrides,
  };
}

export function freeAvatarStatus(overrides?: Partial<AvatarStatusResult>): AvatarStatusResult {
  return avatarStatus({ tier: 'Free', dailyMessageLimit: 5, messagesRemainingToday: 5, ...overrides });
}

export function avatarReply(overrides?: Partial<AvatarReplyResult>): AvatarReplyResult {
  return {
    conversationId: avatarConversationId,
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

export function myAvatarConversation(
  overrides?: Partial<StudentAvatarConversationResult>,
): StudentAvatarConversationResult {
  return {
    id: myAvatarConversationId,
    entryPoint: 'Lesson',
    subjectId: null,
    subjectName: 'الفيزياء',
    lessonId: browseLessonId,
    lessonName: 'قانون أوم',
    sessionId: null,
    questionId: null,
    startedAt: '2026-10-01T12:00:00Z',
    lastMessageAt: '2026-10-01T12:05:00Z',
    messageCount: 2,
    firstQuestion: 'ما هو قانون أوم؟',
    ...overrides,
  };
}

export function myAvatarConversationsPage(
  items: StudentAvatarConversationResult[],
  overrides?: Partial<PageDataOfStudentAvatarConversationResult>,
): PageDataOfStudentAvatarConversationResult {
  return { items, pageNumber: 1, pageSize: 20, totalItems: items.length, totalPages: 1, ...overrides };
}

export function myAvatarConversationDetail(
  overrides?: Partial<StudentAvatarConversationDetailResult>,
): StudentAvatarConversationDetailResult {
  const {
    id,
    entryPoint,
    subjectId,
    subjectName,
    lessonId,
    lessonName,
    sessionId,
    questionId,
    startedAt,
    lastMessageAt,
  } = myAvatarConversation();
  return {
    id,
    entryPoint,
    subjectId,
    subjectName,
    lessonId,
    lessonName,
    sessionId,
    questionId,
    startedAt,
    lastMessageAt,
    messageCount: 2,
    messages: [
      {
        id: 'b0a1c2d3-0000-4000-8000-000000000001',
        position: 0,
        role: 'Student',
        text: 'ما هو قانون أوم؟',
        createdAt: startedAt,
        citations: [],
      },
      {
        id: 'b0a1c2d3-0000-4000-8000-000000000002',
        position: 1,
        role: 'Assistant',
        text: avatarReply().reply,
        createdAt: lastMessageAt,
        citations: avatarReply().citations,
      },
    ],
    ...overrides,
  };
}
