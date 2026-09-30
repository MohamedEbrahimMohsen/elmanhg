import { z } from 'zod';

export const gradeReviewSearchSchema = z.object({
  subjectId: z.guid().optional().catch(undefined),
  kind: z.enum(['Essay', 'MathSteps']).catch('Essay'),
  page: z.coerce.number().int().min(1).optional().catch(undefined),
});

export type GradeReviewSearch = z.infer<typeof gradeReviewSearchSchema>;
