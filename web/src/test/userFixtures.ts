import type {
  AdminSubscriptionResult,
  PageDataOfSessionHistoryItemResult,
  PageDataOfUserSummaryResult,
  SessionHistoryItemResult,
  StudentProfileResult,
  StudentProgressResult,
  UserSummaryResult,
} from '@/shared/api/generated/model';

export const listStudentId = 'c1c1c1c1-c1c1-4c1c-8c1c-c1c1c1c1c1c1';
export const listTeacherId = 'c2c2c2c2-c2c2-4c2c-8c2c-c2c2c2c2c2c2';
export const listAdminId = 'c3c3c3c3-c3c3-4c3c-8c3c-c3c3c3c3c3c3';

export function userSummary(overrides: Partial<UserSummaryResult> = {}): UserSummaryResult {
  return {
    id: listStudentId,
    displayName: 'Mona Ali',
    role: 'Student',
    status: 'Active',
    maskedPhone: '010*****678',
    maskedEmail: null,
    invitationPending: false,
    canSuspend: true,
    creationDate: '2026-09-01T10:00:00Z',
    tier: 'Free',
    hasAskTeacher: false,
    subjectIds: [],
    ...overrides,
  };
}

export function usersPage(items: UserSummaryResult[], pageNumber = 1, totalPages = 1): PageDataOfUserSummaryResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

export function adminSubscription(overrides: Partial<AdminSubscriptionResult> = {}): AdminSubscriptionResult {
  return {
    id: 'd1d1d1d1-d1d1-4d1d-8d1d-d1d1d1d1d1d1',
    plan: 'Base',
    period: 'Monthly',
    status: 'Active',
    currentPeriodStart: '2026-09-01T10:00:00Z',
    currentPeriodEnd: '2026-10-01T10:00:00Z',
    entitledUntil: '2026-10-04T10:00:00Z',
    isComplimentary: false,
    ...overrides,
  };
}

export function studentProfile(overrides: Partial<StudentProfileResult> = {}): StudentProfileResult {
  return {
    id: listStudentId,
    displayName: 'Mona Ali',
    maskedPhone: '010*****678',
    maskedEmail: null,
    status: 'Active',
    canSuspend: true,
    creationDate: '2026-09-01T10:00:00Z',
    onboardedAt: '2026-09-02T10:00:00Z',
    subjectInterests: ['Physics'],
    tier: 'Free',
    hasAskTeacher: false,
    subscriptions: [],
    ...overrides,
  };
}

export function studentProgress(overrides: Partial<StudentProgressResult> = {}): StudentProgressResult {
  return {
    subjects: [
      {
        subjectId: 'e1e1e1e1-e1e1-4e1e-8e1e-e1e1e1e1e1e1',
        name: 'Physics',
        servableCount: 20,
        masteredCount: 8,
        seenCount: 12,
        masteryPercent: 40,
        units: [
          {
            unitId: 'e2e2e2e2-e2e2-4e2e-8e2e-e2e2e2e2e2e2',
            name: 'Mechanics',
            servableCount: 20,
            masteredCount: 8,
            seenCount: 12,
            masteryPercent: 40,
            bestExamScorePercent: 75,
          },
        ],
      },
    ],
    weakSpots: {
      lessons: [
        {
          lessonId: 'e3e3e3e3-e3e3-4e3e-8e3e-e3e3e3e3e3e3',
          lessonName: "Newton's laws",
          subjectId: 'e1e1e1e1-e1e1-4e1e-8e1e-e1e1e1e1e1e1',
          subjectName: 'Physics',
          servableCount: 10,
          masteredCount: 2,
          seenCount: 5,
          masteryPercent: 20,
        },
      ],
      objectives: [],
    },
    ...overrides,
  };
}

export function studentHistoryItem(overrides: Partial<SessionHistoryItemResult> = {}): SessionHistoryItemResult {
  return {
    id: 'f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1',
    kind: 'Quiz',
    lessonId: 'e3e3e3e3-e3e3-4e3e-8e3e-e3e3e3e3e3e3',
    unitId: null,
    scopeName: "Newton's laws",
    startedAt: '2026-09-20T10:00:00Z',
    submittedAt: '2026-09-20T10:10:00Z',
    scorePercent: 80,
    isBestScore: false,
    ...overrides,
  };
}

export function studentHistoryPage(items: SessionHistoryItemResult[]): PageDataOfSessionHistoryItemResult {
  return { items, pageNumber: 1, pageSize: 20, totalItems: items.length, totalPages: items.length > 0 ? 1 : 0 };
}
