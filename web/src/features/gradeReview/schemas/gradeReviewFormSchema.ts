import { z } from 'zod';
import { gradeReviewCommentMaxLength } from '../api/gradeReviewOptions';

const scorePattern = /^\d+(\.\d{1,2})?$/;

export function gradeReviewFormSchema(maxScore: number) {
  return z
    .object({
      decision: z.enum(['Accepted', 'Overridden']),
      score: z.string(),
      comment: z.string().max(gradeReviewCommentMaxLength, { error: 'form.commentTooLong' }),
    })
    .superRefine((values, context) => {
      if (values.decision !== 'Overridden') {
        return;
      }
      const score = values.score.trim();
      if (score === '') {
        context.addIssue({ code: 'custom', path: ['score'], message: 'form.scoreRequired' });
      } else if (!scorePattern.test(score) || Number(score) > maxScore) {
        context.addIssue({ code: 'custom', path: ['score'], message: 'form.scoreRange' });
      }
      if (values.comment.trim() === '') {
        context.addIssue({ code: 'custom', path: ['comment'], message: 'form.commentRequired' });
      }
    });
}

export type GradeReviewFormValues = z.infer<ReturnType<typeof gradeReviewFormSchema>>;
