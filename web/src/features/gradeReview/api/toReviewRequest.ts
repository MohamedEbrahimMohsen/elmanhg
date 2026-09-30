import type { ReviewGradeRequest } from '@/shared/api/generated/model';
import type { GradeReviewFormValues } from '../schemas/gradeReviewFormSchema';

export function toReviewRequest(values: GradeReviewFormValues): ReviewGradeRequest {
  const comment = values.comment.trim();
  return {
    decision: values.decision,
    score: values.decision === 'Overridden' ? Number(values.score.trim()) : null,
    comment: comment === '' ? null : comment,
  };
}
