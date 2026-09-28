import type {
  GetValidationQueueParams,
  PageDataOfValidationQueueItemResult,
  ValidationQueueItemResult,
} from '@/shared/api/generated/model';
import type { ValidationQueueSearch } from '../schemas/validationQueueSearchSchema';
import { validationQueuePageSize } from './questionOptions';

export interface ValidationQueuePage {
  items: ValidationQueueItemResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

export function toValidationQueueParams(
  search: ValidationQueueSearch,
  reviewSessionId: string,
): GetValidationQueueParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: validationQueuePageSize,
    ...(search.unitId ? { unitId: search.unitId } : {}),
    ...(search.lessonId ? { lessonId: search.lessonId } : {}),
    ...(search.type ? { type: search.type } : {}),
    ...(search.difficulty ? { difficulty: search.difficulty } : {}),
    ...(search.minAgeDays ? { minAgeDays: search.minAgeDays } : {}),
    reviewSessionId,
  };
}

export function hasActiveValidationFilters(search: ValidationQueueSearch): boolean {
  return [search.unitId, search.lessonId, search.type, search.difficulty, search.minAgeDays].some(
    (value) => value !== undefined,
  );
}

export function toValidationQueuePage(data: PageDataOfValidationQueueItemResult): ValidationQueuePage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
