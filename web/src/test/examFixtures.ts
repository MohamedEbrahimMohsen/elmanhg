import type {
  ExamItemResult,
  ExamSessionResult,
  MultiUnitExamOverviewResult,
  MultiUnitExamPreviewResult,
  UnitExamOverviewResult,
} from '@/shared/api/generated/model';

export const examSessionId = '12121212-1212-4121-8121-121212121212';
export const examUnitId = '34343434-3434-4343-8343-343434343434';
export const examLessonId = '56565656-5656-4565-8565-565656565656';
export const examSubjectId = '90909090-9090-4909-8909-909090909090';
export const examSecondUnitId = '45454545-4545-4454-8454-454545454545';

export function examItem(position: number, overrides?: Partial<ExamItemResult>): ExamItemResult {
  return {
    position,
    questionId: `00000000-0000-4000-8000-00000000000${String(position)}`,
    questionVersion: 1,
    type: 'Mcq',
    stem: `<p>Question ${String(position)} stem</p>`,
    body: {
      options: [
        { id: 'a', text: '<p>3</p>' },
        { id: 'b', text: '<p>4</p>' },
        { id: 'c', text: '<p>5</p>' },
      ],
    },
    maxScore: 1,
    savedAnswer: null,
    answerSavedAt: null,
    attempt: null,
    correctAnswer: null,
    explanation: null,
    ...overrides,
  };
}

export function openExam(items: ExamItemResult[], overrides?: Partial<ExamSessionResult>): ExamSessionResult {
  return {
    id: examSessionId,
    kind: 'UnitExam',
    isTestMode: false,
    subjectId: examSubjectId,
    subjectName: 'Physics',
    units: [{ unitId: examUnitId, name: 'Mechanics' }],
    startedAt: '2026-09-28T10:00:00Z',
    timeLimitMinutes: 30,
    deadline: '2026-09-28T10:30:00Z',
    serverNow: '2026-09-28T10:00:01Z',
    passMark: 50,
    submittedAt: null,
    scorePercent: null,
    isPassed: null,
    elapsedMilliseconds: 0,
    items,
    lessons: [],
    unitBreakdown: [],
    weakestObjectives: [],
    ...overrides,
  };
}

export function submittedExam(items: ExamItemResult[], overrides?: Partial<ExamSessionResult>): ExamSessionResult {
  return openExam(
    items.map((item) => ({ ...item, correctAnswer: { correctOptionId: 'b' }, explanation: '<p>Four.</p>' })),
    {
      submittedAt: '2026-09-28T10:12:30Z',
      scorePercent: 80,
      isPassed: true,
      elapsedMilliseconds: 750_000,
      lessons: [
        {
          lessonId: examLessonId,
          name: "Newton's laws",
          questionCount: 2,
          correctCount: 1,
          score: 1,
          maxScore: 2,
          scorePercent: 50,
        },
      ],
      weakestObjectives: [
        {
          objectiveId: '78787878-7878-4787-8787-787878787878',
          text: 'State the first law',
          lessonId: examLessonId,
          lessonName: "Newton's laws",
          questionCount: 1,
          scorePercent: 0,
        },
      ],
      ...overrides,
    },
  );
}

export function overview(overrides?: Partial<UnitExamOverviewResult>): UnitExamOverviewResult {
  return {
    unitId: examUnitId,
    unitName: 'Mechanics',
    subjectId: examSubjectId,
    subjectName: 'Physics',
    blueprint: {
      isSubjectDefault: false,
      questionCount: 10,
      typeCounts: [{ type: 'Mcq', required: 10, available: 12 }],
      difficultyMix: null,
      timeLimitMinutes: 45,
      passMark: 50,
    },
    isAvailable: true,
    inProgressExam: null,
    ...overrides,
  };
}

export function multiOverview(overrides?: Partial<MultiUnitExamOverviewResult>): MultiUnitExamOverviewResult {
  return {
    subjectId: examSubjectId,
    subjectName: 'Physics',
    units: [
      { unitId: examUnitId, name: 'Mechanics', hasBlueprint: true, isSubjectDefault: false, servableCount: 12 },
      { unitId: examSecondUnitId, name: 'Waves', hasBlueprint: true, isSubjectDefault: false, servableCount: 13 },
    ],
    sizes: [20, 40, 60],
    inProgressExam: null,
    ...overrides,
  };
}

export function multiPreview(overrides?: Partial<MultiUnitExamPreviewResult>): MultiUnitExamPreviewResult {
  return {
    subjectId: examSubjectId,
    size: 20,
    blueprint: {
      isSubjectDefault: false,
      questionCount: 20,
      typeCounts: [{ type: 'Mcq', required: 20, available: 25 }],
      difficultyMix: null,
      timeLimitMinutes: 47,
      passMark: 57,
    },
    isAvailable: true,
    units: [
      { unitId: examUnitId, name: 'Mechanics', questionCount: 10, isSubjectDefault: false },
      { unitId: examSecondUnitId, name: 'Waves', questionCount: 10, isSubjectDefault: false },
    ],
    ...overrides,
  };
}

export function multiExam(items: ExamItemResult[], overrides?: Partial<ExamSessionResult>): ExamSessionResult {
  return submittedExam(items, {
    kind: 'MultiUnitExam',
    units: [
      { unitId: examUnitId, name: 'Mechanics' },
      { unitId: examSecondUnitId, name: 'Waves' },
    ],
    unitBreakdown: [
      {
        unitId: examUnitId,
        name: 'Mechanics',
        questionCount: 1,
        correctCount: 1,
        score: 1,
        maxScore: 1,
        scorePercent: 100,
      },
      {
        unitId: examSecondUnitId,
        name: 'Waves',
        questionCount: 1,
        correctCount: 0,
        score: 0,
        maxScore: 1,
        scorePercent: 0,
      },
    ],
    ...overrides,
  });
}
