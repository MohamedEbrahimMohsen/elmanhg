import type { QuestionDifficulty, QuestionType, QuestionValidationStatus } from '@/shared/api/generated/model';

export const questionTypes = ['Mcq', 'Multi', 'TrueFalse', 'Fill', 'Short'] as const satisfies readonly QuestionType[];

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

// mirrors Content:QuestionFilterMaxLength
export const questionFilterMaxLength = 200;

export const questionListPageSize = 20;

// mirrors QuestionValidation:RejectionReasonMaxLength
export const rejectionReasonMaxLength = 1000;

export const validationAgeFilters = [1, 3, 7] as const;

export const validationQueuePageSize = 20;

export const optionIdAlphabet = 'abcdefghij';

function isOneOf<T extends string>(values: readonly T[], value: string): value is T {
  return (values as readonly string[]).includes(value);
}

export function toQuestionType(value: string): QuestionType {
  return isOneOf(questionTypes, value) ? value : 'Mcq';
}

export function toQuestionDifficulty(value: string): QuestionDifficulty {
  return isOneOf(questionDifficulties, value) ? value : 'Medium';
}
