import { useGetGradeReviewQueue } from '@/shared/api/generated/grade-reviews/grade-reviews';
import { gradeReviewPageSize, type GradeReviewKindValue } from '../api/gradeReviewOptions';

export function useGradeReviewQueue(
  subjectId: string | undefined,
  kind: GradeReviewKindValue,
  page: number | undefined,
) {
  return useGetGradeReviewQueue(
    subjectId ?? '',
    { kind, pageNumber: page ?? 1, pageSize: gradeReviewPageSize },
    { query: { enabled: subjectId !== undefined } },
  );
}
