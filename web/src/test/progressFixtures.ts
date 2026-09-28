import type {
  PageDataOfSessionHistoryItemResult,
  SessionHistoryItemResult,
  SubjectProgressResult,
  WeakSpotsResult,
} from '@/shared/api/generated/model';

export const physicsSubjectId = '11111111-1111-4111-8111-111111111111';
export const chemistrySubjectId = '22222222-2222-4222-8222-222222222222';
export const algebraUnitId = '33333333-3333-4333-8333-333333333333';
export const mechanicsUnitId = '44444444-4444-4444-8444-444444444444';
export const wavesUnitId = '55555555-5555-4555-8555-555555555555';
export const weakLessonId = '66666666-6666-4666-8666-666666666666';
export const weakObjectiveLessonId = weakLessonId;
export const weakObjectiveId = '77777777-7777-4777-8777-777777777777';
export const finishedQuizId = '88888888-8888-4888-8888-888888888888';
export const openQuizId = '99999999-9999-4999-8999-999999999999';
export const examSessionId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

export function subjectProgress(overrides?: SubjectProgressResult[]): SubjectProgressResult[] {
  return (
    overrides ?? [
      {
        subjectId: physicsSubjectId,
        name: 'Physics',
        servableCount: 10,
        masteredCount: 4,
        seenCount: 6,
        masteryPercent: 40,
        units: [
          {
            unitId: mechanicsUnitId,
            name: 'Mechanics',
            servableCount: 8,
            masteredCount: 4,
            seenCount: 6,
            masteryPercent: 50,
            bestExamScorePercent: 87.5,
          },
          {
            unitId: wavesUnitId,
            name: 'Waves',
            servableCount: 2,
            masteredCount: 0,
            seenCount: 0,
            masteryPercent: 0,
            bestExamScorePercent: null,
          },
        ],
      },
      {
        subjectId: chemistrySubjectId,
        name: 'Chemistry',
        servableCount: 5,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
        units: [
          {
            unitId: algebraUnitId,
            name: 'Atoms',
            servableCount: 5,
            masteredCount: 0,
            seenCount: 0,
            masteryPercent: 0,
            bestExamScorePercent: null,
          },
        ],
      },
    ]
  );
}

export function weakSpots(overrides?: Partial<WeakSpotsResult>): WeakSpotsResult {
  return {
    lessons: [
      {
        lessonId: weakLessonId,
        lessonName: "Ohm's law",
        subjectId: physicsSubjectId,
        subjectName: 'Physics',
        servableCount: 5,
        masteredCount: 1,
        seenCount: 3,
        masteryPercent: 20,
      },
    ],
    objectives: [
      {
        objectiveId: weakObjectiveId,
        text: "State Ohm's law",
        lessonId: weakObjectiveLessonId,
        lessonName: "Ohm's law",
        subjectId: physicsSubjectId,
        subjectName: 'Physics',
        servableCount: 2,
        masteredCount: 0,
        seenCount: 1,
        masteryPercent: 0,
      },
    ],
    ...overrides,
  };
}

export const openQuizItem: SessionHistoryItemResult = {
  id: openQuizId,
  kind: 'Quiz',
  lessonId: weakLessonId,
  unitId: null,
  scopeName: "Ohm's law",
  startedAt: '2026-09-20T09:00:00Z',
  submittedAt: null,
  scorePercent: null,
  isBestScore: false,
};

export const finishedQuizItem: SessionHistoryItemResult = {
  id: finishedQuizId,
  kind: 'Quiz',
  lessonId: '12121212-1212-4212-8212-121212121212',
  unitId: null,
  scopeName: "Newton's laws",
  startedAt: '2026-09-19T09:00:00Z',
  submittedAt: '2026-09-19T09:20:00Z',
  scorePercent: 72.4,
  isBestScore: false,
};

export const examItem: SessionHistoryItemResult = {
  id: examSessionId,
  kind: 'UnitExam',
  lessonId: null,
  unitId: mechanicsUnitId,
  scopeName: 'Mechanics',
  startedAt: '2026-09-18T09:00:00Z',
  submittedAt: '2026-09-18T10:00:00Z',
  scorePercent: 90,
  isBestScore: false,
};

export function sessionHistoryPage(
  items: SessionHistoryItemResult[] = [openQuizItem, finishedQuizItem, examItem],
  overrides?: Partial<PageDataOfSessionHistoryItemResult>,
): PageDataOfSessionHistoryItemResult {
  return { items, pageNumber: 1, pageSize: 20, totalItems: items.length, totalPages: 1, ...overrides };
}
