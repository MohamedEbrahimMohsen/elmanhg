export const gradeReviewKinds = ['Essay', 'MathSteps'] as const;

export type GradeReviewKindValue = (typeof gradeReviewKinds)[number];

// mirrors GradeReview:CommentMaxLength
export const gradeReviewCommentMaxLength = 2000;

export const gradeReviewPageSize = 20;

export const kindSegments = { Essay: 'essay', MathSteps: 'math-steps' } as const;

export function kindFromSegment(segment: string): GradeReviewKindValue | null {
  if (segment === kindSegments.Essay) {
    return 'Essay';
  }
  if (segment === kindSegments.MathSteps) {
    return 'MathSteps';
  }
  return null;
}

export function toKind(value: string): GradeReviewKindValue {
  return value === 'MathSteps' ? 'MathSteps' : 'Essay';
}
