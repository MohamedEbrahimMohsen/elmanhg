import type {
  AdminAvatarConversationDetailResult,
  AdminAvatarConversationResult,
  PageDataOfAdminAvatarConversationResult,
} from '@/shared/api/generated/model';

export function conversationItem(overrides?: Partial<AdminAvatarConversationResult>): AdminAvatarConversationResult {
  return {
    id: 'c1',
    studentId: 's1',
    studentName: 'Sara Ahmed',
    entryPoint: 'Lesson',
    subjectId: 'sub1',
    subjectName: 'Physics',
    lessonId: 'l1',
    lessonName: "Ohm's law",
    questionId: null,
    startedAt: '2026-10-01T09:00:00Z',
    lastMessageAt: '2026-10-01T09:05:00Z',
    messageCount: 4,
    firstQuestion: 'What is resistance?',
    ...overrides,
  };
}

export function conversationPage(
  items: AdminAvatarConversationResult[],
  pageNumber = 1,
  totalPages = 1,
): PageDataOfAdminAvatarConversationResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

export function conversationDetail(
  overrides?: Partial<AdminAvatarConversationDetailResult>,
): AdminAvatarConversationDetailResult {
  return {
    id: 'c1',
    studentId: 's1',
    studentName: 'Sara Ahmed',
    entryPoint: 'Lesson',
    subjectId: 'sub1',
    subjectName: 'Physics',
    unitId: 'u1',
    unitName: 'Electricity',
    lessonId: 'l1',
    lessonName: "Ohm's law",
    sessionId: null,
    questionId: null,
    startedAt: '2026-10-01T09:00:00Z',
    lastMessageAt: '2026-10-01T09:00:05Z',
    messageCount: 2,
    totalInputTokens: 100,
    totalOutputTokens: 20,
    totalCostUsd: 0.0021,
    messages: [
      {
        id: 'm0',
        position: 0,
        role: 'Student',
        text: 'What is resistance?',
        createdAt: '2026-10-01T09:00:00Z',
        model: null,
        promptVersion: null,
        inputTokens: null,
        outputTokens: null,
        costUsd: null,
        stopReason: null,
        historyMessageCount: null,
        citations: [],
        context: null,
      },
      {
        id: 'm1',
        position: 1,
        role: 'Assistant',
        text: 'R = V / I',
        createdAt: '2026-10-01T09:00:05Z',
        model: 'claude-sonnet-5',
        promptVersion: 'v2',
        inputTokens: 100,
        outputTokens: 20,
        costUsd: 0.0021,
        stopReason: 'end_turn',
        historyMessageCount: 0,
        citations: [
          {
            reference: 'explanation-1',
            section: 'Explanation',
            sectionTitle: 'قانون أوم',
            lessonId: 'l1',
            questionId: null,
          },
        ],
        context: {
          bundle: {
            entryPoint: 'Lesson',
            subject: { id: 'sub1', name: 'Physics' },
            unit: { id: 'u1', name: 'Electricity' },
            lesson: { id: 'l1', name: "Ohm's law", explanation: '', objectives: [], summary: '' },
            question: null,
            subjects: [],
          },
          sources: [{ reference: 'explanation-1', title: 'الشرح — قانون أوم', content: 'V = I R' }],
        },
      },
    ],
    ...overrides,
  };
}
