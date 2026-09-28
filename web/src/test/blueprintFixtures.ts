import type {
  ExamBlueprintResult,
  ExamTypeCountResult,
  QuestionType,
  SubjectExamBlueprintsResult,
} from '@/shared/api/generated/model';

export const blueprintSubjectId = '11111111-1111-4111-8111-111111111111';
export const mathSubjectId = '22222222-2222-4222-8222-222222222222';
export const mechanicsUnitId = '33333333-3333-4333-8333-333333333333';
export const defaultBlueprintId = '44444444-4444-4444-8444-444444444444';
export const unitBlueprintId = '55555555-5555-4555-8555-555555555555';

const allTypes: QuestionType[] = ['Mcq', 'Multi', 'TrueFalse', 'Fill', 'Short'];

export function servable(partial: Partial<Record<QuestionType, number>>): ExamTypeCountResult[] {
  return allTypes.map((type) => ({ type, count: partial[type] ?? 0 }));
}

export function blueprint(overrides?: Partial<ExamBlueprintResult>): ExamBlueprintResult {
  return {
    id: defaultBlueprintId,
    subjectId: blueprintSubjectId,
    unitId: null,
    typeCounts: [{ type: 'Mcq', count: 2 }],
    difficultyMix: null,
    questionCount: 2,
    timeLimitMinutes: 45,
    passMark: 50,
    ...overrides,
  };
}

export function overview(overrides?: Partial<SubjectExamBlueprintsResult>): SubjectExamBlueprintsResult {
  return {
    subjectId: blueprintSubjectId,
    subjectName: 'Physics',
    defaultBlueprint: blueprint(),
    servable: servable({ Mcq: 3, Fill: 1 }),
    units: [
      {
        unitId: mechanicsUnitId,
        name: 'Mechanics',
        order: 1,
        blueprint: null,
        servable: servable({ Mcq: 3, Fill: 1 }),
      },
    ],
    ...overrides,
  };
}
