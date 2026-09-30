import type { QuestionDifficulty, QuestionType, QuestionValidationStatus } from '@/shared/api/generated/model';

export const questionTypes = [
  'Mcq',
  'Multi',
  'TrueFalse',
  'Fill',
  'Short',
  'Essay',
  'MathSteps',
  'DragDrop',
] as const satisfies readonly QuestionType[];

export const servedQuestionTypes = [
  'Mcq',
  'Multi',
  'TrueFalse',
  'Fill',
  'Short',
  'Essay',
  'MathSteps',
  'DragDrop',
] as const satisfies readonly QuestionType[];

export const questionDifficulties = ['Easy', 'Medium', 'Hard'] as const satisfies readonly QuestionDifficulty[];

export const validationStatuses = [
  'Pending',
  'Approved',
  'Rejected',
] as const satisfies readonly QuestionValidationStatus[];

// mirrors Content:QuestionMaxScoreMax
export const questionMaxScoreMax = 100;

export const questionOptionsMin = 2;

// mirrors Content:QuestionOptionsMaxCount
export const questionOptionsMax = 10;

// mirrors Content:QuestionBlanksMaxCount
export const questionBlanksMax = 10;

// mirrors Content:QuestionEssayMaxWordsMax
export const essayMaxWordsMax = 2000;

// Mirrors Content:QuestionEssayAnswerMaxLength.
export const essayAnswerMaxLength = 20000;

export const essayCharactersLeftShownAt = 1000;

// mirrors Content:QuestionRubricCriteriaMaxCount
export const rubricCriteriaMax = 10;

export const rubricLevelsMin = 2;

// mirrors Content:QuestionRubricLevelsMaxCount
export const rubricLevelsMax = 6;

// mirrors Content:QuestionRubricPointsMax
export const rubricPointsMax = 100;

// mirrors Content:QuestionModelAnswersMaxCount
export const modelAnswersMax = 3;

export const mathAnswerForms = ['equivalent', 'simplified', 'factored', 'expanded', 'exact'] as const;

// mirrors Content:QuestionAcceptedAnswersMaxCount
export const mathAnswersMax = 20;

// mirrors Content:QuestionAnswerMaxLength
export const mathAnswerMaxLength = 200;

// mirrors Content:QuestionDiagramZonesMaxCount
export const diagramZonesMax = 20;

// mirrors Content:QuestionDiagramItemsMaxCount
export const diagramItemsMax = 30;

// mirrors Content:QuestionDiagramZoneCapacityMax
export const diagramZoneCapacityMax = 10;

// mirrors Content:QuestionDiagramZoneMinSizePercent
export const diagramZoneMinSize = 2;

// mirrors Content:QuestionDiagramItemTextMaxLength
export const diagramItemTextMax = 100;

// mirrors TeacherThreadImageFormats (diagram uploads)
export const acceptedDiagramImageTypes = 'image/png,image/jpeg,image/webp';

// mirrors Content:QuestionFilterMaxLength
export const questionFilterMaxLength = 200;

export const questionListPageSize = 20;

// mirrors QuestionValidation:RejectionReasonMaxLength
export const rejectionReasonMaxLength = 1000;

export const validationAgeFilters = [1, 3, 7] as const;

export const validationQueuePageSize = 20;

export const optionIdAlphabet = 'abcdefghij';

export const normalizationRules = [
  'stripTashkeel',
  'stripTatweel',
  'unifyAlef',
  'unifyTaaMarbuta',
  'unifyAlefMaqsura',
  'convertDigits',
  'collapseWhitespace',
  'foldCase',
] as const;

export type NormalizationRule = (typeof normalizationRules)[number];

function isOneOf<T extends string>(values: readonly T[], value: string): value is T {
  return (values as readonly string[]).includes(value);
}

export function toQuestionType(value: string): QuestionType {
  return isOneOf(questionTypes, value) ? value : 'Mcq';
}

export function toQuestionDifficulty(value: string): QuestionDifficulty {
  return isOneOf(questionDifficulties, value) ? value : 'Medium';
}
