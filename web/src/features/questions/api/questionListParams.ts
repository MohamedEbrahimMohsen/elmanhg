import type {
  GetQuestionsParams,
  PageDataOfQuestionListItemResult,
  QuestionListItemResult,
} from '@/shared/api/generated/model';
import type { QuestionListSearch } from '../schemas/questionListSearchSchema';
import { questionListPageSize } from './questionOptions';

export interface QuestionListPage {
  items: QuestionListItemResult[];
  pageNumber: number;
  totalPages: number;
  totalItems: number;
}

export function toQuestionListParams(search: QuestionListSearch): GetQuestionsParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: questionListPageSize,
    ...(search.status ? { status: search.status } : {}),
    ...(search.type ? { type: search.type } : {}),
    ...(search.subjectId ? { subjectId: search.subjectId } : {}),
    ...(search.lessonId ? { lessonId: search.lessonId } : {}),
    ...(search.teacherId ? { teacherId: search.teacherId } : {}),
    ...(search.minVersion ? { minVersion: search.minVersion } : {}),
    ...(search.rejectionReason ? { rejectionReason: search.rejectionReason } : {}),
  };
}

export function hasActiveFilters(search: QuestionListSearch): boolean {
  return [
    search.status,
    search.type,
    search.subjectId,
    search.lessonId,
    search.teacherId,
    search.minVersion,
    search.rejectionReason,
  ].some((value) => value !== undefined);
}

export function toQuestionListPage(data: PageDataOfQuestionListItemResult): QuestionListPage {
  return {
    items: data.items ?? [],
    pageNumber: Number(data.pageNumber ?? 1),
    totalPages: Number(data.totalPages ?? 0),
    totalItems: Number(data.totalItems ?? 0),
  };
}
