import type {
  PageDataOfTeacherThreadSummaryResult,
  SubjectMasteryDetailResult,
  TeacherThreadContextResult,
  TeacherThreadResult,
  TeacherThreadSummaryResult,
  UsageResult,
} from '@/shared/api/generated/model';
import { physicsId } from './masteryFixtures';
import { baseUsage } from './subscriptionFixtures';

export const threadId = 'f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1';
export const attemptId = 'aaaaaaaa-aaaa-4aaa-8aaa-000000000001';
export const contextLessonId = 'e2e2e2e2-e2e2-4e2e-8e2e-e2e2e2e2e2e2';
export const firstUnitLessonId = 'e3e3e3e3-e3e3-4e3e-8e3e-e3e3e3e3e3e3';
export const secondUnitLessonId = 'e4e4e4e4-e4e4-4e4e-8e4e-e4e4e4e4e4e4';
export const threadImageUrl = '/api/media/teacher-threads/0123456789abcdef0123456789abcdef.png';

export function threadContext(overrides?: Partial<TeacherThreadContextResult>): TeacherThreadContextResult {
  return {
    subjectId: physicsId,
    subjectName: 'Physics',
    unitId: 'e1e1e1e1-e1e1-4e1e-8e1e-e1e1e1e1e1e1',
    unitName: 'Mechanics',
    lessonId: contextLessonId,
    lessonName: "Newton's laws",
    questionId: null,
    questionVersion: null,
    questionStem: null,
    attemptId: null,
    ...overrides,
  };
}

export function threadSummary(overrides?: Partial<TeacherThreadSummaryResult>): TeacherThreadSummaryResult {
  return {
    id: threadId,
    subjectName: 'Physics',
    lessonName: "Newton's laws",
    questionText: 'Why is F = ma?',
    status: 'Open',
    isOverdue: false,
    submittedAt: '2026-10-01T07:00:00Z',
    slaDueAt: '2026-10-02T07:00:00Z',
    ...overrides,
  };
}

export function threadsPage(
  items: TeacherThreadSummaryResult[],
  { pageNumber = 1, totalPages = 1 }: { pageNumber?: number; totalPages?: number } = {},
): PageDataOfTeacherThreadSummaryResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

export function teacherThread(overrides?: Partial<TeacherThreadResult>): TeacherThreadResult {
  return {
    id: threadId,
    context: threadContext(),
    status: 'Open',
    isOverdue: false,
    submittedAt: '2026-10-01T07:00:00Z',
    slaDueAt: '2026-10-02T07:00:00Z',
    messages: [
      {
        id: 'f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2',
        isFromStudent: true,
        kind: 'Text',
        text: 'Why is F = ma?',
        imageUrl: threadImageUrl,
        createdAt: '2026-10-01T07:00:00Z',
      },
    ],
    ...overrides,
  };
}

export function askTeacherUsage(overrides?: Partial<UsageResult>): UsageResult {
  return {
    ...baseUsage(),
    hasAskTeacher: true,
    monthlyAskTeacherQuestionLimit: 20,
    askTeacherQuestionsUsedThisMonth: 3,
    askTeacherQuestionsRemainingThisMonth: 17,
    ...overrides,
  };
}

function lesson(lessonId: string, name: string) {
  return { lessonId, name, servableCount: 5, masteredCount: 0, seenCount: 0, masteryPercent: 0 };
}

export function subjectMasteryDetail(): SubjectMasteryDetailResult {
  return {
    subjectId: physicsId,
    name: 'Physics',
    servableCount: 10,
    masteredCount: 0,
    seenCount: 0,
    masteryPercent: 0,
    units: [
      {
        unitId: 'e5e5e5e5-e5e5-4e5e-8e5e-e5e5e5e5e5e5',
        name: 'Mechanics',
        servableCount: 5,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
        lessons: [lesson(firstUnitLessonId, "Newton's laws")],
      },
      {
        unitId: 'e6e6e6e6-e6e6-4e6e-8e6e-e6e6e6e6e6e6',
        name: 'Waves',
        servableCount: 5,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
        lessons: [lesson(secondUnitLessonId, 'Sound')],
      },
    ],
  };
}
