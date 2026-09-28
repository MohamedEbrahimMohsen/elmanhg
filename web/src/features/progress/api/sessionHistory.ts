import type { GetSessionHistoryParams, SessionHistoryItemResult } from '@/shared/api/generated/model';
import type { ProgressSearch } from '../schemas/progressSearchSchema';

export const historyPageSize = 20;

export type SessionLink = {
  to:
    | '/student/quiz-result/$sessionId'
    | '/student/quiz/$sessionId'
    | '/student/exam-result/$sessionId'
    | '/student/exam/$sessionId';
  labelKey: 'history.view' | 'history.continue';
} | null;

export function toSessionHistoryParams(search: ProgressSearch): GetSessionHistoryParams {
  return {
    pageNumber: search.page ?? 1,
    pageSize: historyPageSize,
    ...(search.kind ? { kind: search.kind } : {}),
  };
}

export function sessionLink(item: SessionHistoryItemResult): SessionLink {
  if (item.kind !== 'Quiz') {
    return item.submittedAt
      ? { to: '/student/exam-result/$sessionId', labelKey: 'history.view' }
      : { to: '/student/exam/$sessionId', labelKey: 'history.continue' };
  }
  return item.submittedAt
    ? { to: '/student/quiz-result/$sessionId', labelKey: 'history.view' }
    : { to: '/student/quiz/$sessionId', labelKey: 'history.continue' };
}

export function sessionKindLabelKey(kind: string): string {
  switch (kind) {
    case 'Quiz':
      return 'history.kindQuiz';
    case 'UnitExam':
      return 'history.kindUnitExam';
    default:
      return 'history.kindMultiUnitExam';
  }
}
