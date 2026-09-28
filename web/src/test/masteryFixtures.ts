import type { MasteryOverviewResult } from '@/shared/api/generated/model';

export const physicsId = '66666666-6666-4666-8666-666666666666';
export const chemistryId = '77777777-7777-4777-8777-777777777777';
export const nextLessonId = '88888888-8888-4888-8888-888888888888';

export function masteryOverview(overrides?: Partial<MasteryOverviewResult>): MasteryOverviewResult {
  return {
    headline: { servableTotal: 60, masteredCount: 20, remainingCount: 40, seenCount: 30 },
    streakDays: 3,
    nextLesson: {
      lessonId: nextLessonId,
      lessonName: "Newton's laws",
      subjectId: physicsId,
      subjectName: 'Physics',
      masteryPercent: 10,
    },
    subjects: [
      {
        subjectId: physicsId,
        name: 'Physics',
        servableCount: 50,
        masteredCount: 20,
        seenCount: 30,
        masteryPercent: 40,
      },
      {
        subjectId: chemistryId,
        name: 'Chemistry',
        servableCount: 10,
        masteredCount: 0,
        seenCount: 0,
        masteryPercent: 0,
      },
    ],
    ...overrides,
  };
}
