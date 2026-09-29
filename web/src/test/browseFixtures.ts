import type { StudentLessonResult, StudentSubjectResult, StudentUnitResult } from '@/shared/api/generated/model';

export const browseSubjectId = 'a1a1a1a1-a1a1-4a1a-8a1a-a1a1a1a1a1a1';
export const browseUnitId = 'b2b2b2b2-b2b2-4b2b-8b2b-b2b2b2b2b2b2';
export const browseSecondUnitId = 'c3c3c3c3-c3c3-4c3c-8c3c-c3c3c3c3c3c3';
export const browseLessonId = 'd4d4d4d4-d4d4-4d4d-8d4d-d4d4d4d4d4d4';
export const browsePreviousLessonId = 'e5e5e5e5-e5e5-4e5e-8e5e-e5e5e5e5e5e5';
export const browseNextLessonId = 'f6f6f6f6-f6f6-4f6f-8f6f-f6f6f6f6f6f6';

export function studentSubject(overrides?: Partial<StudentSubjectResult>): StudentSubjectResult {
  return {
    id: browseSubjectId,
    name: 'Physics',
    servableCount: 10,
    masteredCount: 4,
    seenCount: 6,
    masteryPercent: 40,
    units: [
      {
        id: browseUnitId,
        name: 'Mechanics',
        lessonCount: 2,
        servableCount: 6,
        masteredCount: 3,
        seenCount: 4,
        masteryPercent: 50,
        bestExamScorePercent: 72.4,
      },
      {
        id: browseSecondUnitId,
        name: 'Waves',
        lessonCount: 0,
        servableCount: 0,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
        bestExamScorePercent: null,
      },
    ],
    ...overrides,
  };
}

export function studentUnit(overrides?: Partial<StudentUnitResult>): StudentUnitResult {
  return {
    id: browseUnitId,
    name: 'Mechanics',
    subjectId: browseSubjectId,
    subjectName: 'Physics',
    servableCount: 6,
    masteredCount: 3,
    seenCount: 4,
    masteryPercent: 50,
    bestExamScorePercent: 72.4,
    lessons: [
      {
        id: browsePreviousLessonId,
        name: 'Forces',
        servableCount: 4,
        masteredCount: 2,
        seenCount: 3,
        masteryPercent: 50,
        isLocked: false,
      },
      {
        id: browseLessonId,
        name: 'Energy',
        servableCount: 2,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
        isLocked: false,
      },
    ],
    ...overrides,
  };
}

export function studentLesson(overrides?: Partial<StudentLessonResult>): StudentLessonResult {
  return {
    id: browseLessonId,
    name: 'Energy',
    unitId: browseUnitId,
    unitName: 'Mechanics',
    subjectId: browseSubjectId,
    subjectName: 'Physics',
    explanation: '<p>Energy is conserved.</p>',
    summary: '<p>Energy summary.</p>',
    videoUrl: null,
    objectives: [
      { id: '1a1a1a1a-1a1a-41a1-81a1-1a1a1a1a1a1a', text: 'Define energy', order: 1 },
      { id: '2b2b2b2b-2b2b-42b2-82b2-2b2b2b2b2b2b', text: 'Apply conservation', order: 2 },
    ],
    servableCount: 2,
    masteredCount: 1,
    seenCount: 2,
    masteryPercent: 50,
    previousLesson: { id: browsePreviousLessonId, name: 'Forces', unitId: browseUnitId, unitName: 'Mechanics' },
    nextLesson: { id: browseNextLessonId, name: 'Wave basics', unitId: browseSecondUnitId, unitName: 'Waves' },
    isLocked: false,
    ...overrides,
  };
}
